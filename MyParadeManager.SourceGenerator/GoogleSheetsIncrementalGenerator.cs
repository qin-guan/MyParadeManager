using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MyParadeManager.SourceGenerator;

[Generator]
public class GoogleSheetsIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var entityClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsEntityClass(s),
                transform: static (ctx, _) => GetEntityClassInfo(ctx))
            .Where(static m => m is not null);

        var compilation = context.CompilationProvider.Combine(entityClasses.Collect());
        context.RegisterSourceOutput(compilation, static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static bool IsEntityClass(SyntaxNode syntaxNode)
    {
        return syntaxNode is ClassDeclarationSyntax classDeclaration &&
               classDeclaration.AttributeLists.Count > 0 &&
               classDeclaration.AttributeLists
                   .SelectMany(al => al.Attributes)
                   .Any(attr => attr.Name.ToString().Contains("Entity"));
    }

    private static EntityClassInfo? GetEntityClassInfo(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

        return classSymbol == null ? null : ExtractEntityInfo(classSymbol);
    }

    private static EntityClassInfo? ExtractEntityInfo(INamedTypeSymbol classSymbol)
    {
        var entityAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "EntityAttribute");

        if (entityAttribute == null)
            return null;

        var entityInfo = new EntityClassInfo
        {
            ClassName = classSymbol.Name,
            FullName = classSymbol.ToDisplayString(),
            Namespace = classSymbol.ContainingNamespace.ToDisplayString()
        };

        // Extract entity attribute properties
        foreach (var namedArgument in entityAttribute.NamedArguments)
        {
            switch (namedArgument.Key)
            {
                case "SheetId":
                    entityInfo.SheetId = namedArgument.Value.Value?.ToString();
                    break;
                case "SheetName":
                    entityInfo.SheetName = namedArgument.Value.Value?.ToString();
                    break;
                case "HasHeader":
                    entityInfo.HasHeader = (bool)(namedArgument.Value.Value ?? true);
                    break;
                case "StartRow":
                    entityInfo.StartRow = (int)(namedArgument.Value.Value ?? 1);
                    break;
            }
        }

        // Extract properties
        foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.GetAttributes().Any(a => a.AttributeClass?.Name == "IgnoreAttribute"))
                continue;

            var isNullable = IsPropertyNullable(member);

            var propertyInfo = new PropertyInfo
            {
                Name = member.Name,
                Type = member.Type.ToDisplayString(),
                IsNullable = isNullable
            };

            var columnAttribute = member.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "ColumnAttribute");

            if (columnAttribute != null)
            {
                foreach (var namedArgument in columnAttribute.NamedArguments)
                {
                    switch (namedArgument.Key)
                    {
                        case "Name":
                            propertyInfo.ColumnName = namedArgument.Value.Value?.ToString();
                            break;
                        case "Order":
                            propertyInfo.Order = (int)(namedArgument.Value.Value ?? 0);
                            break;
                        case "ColumnLetter":
                            propertyInfo.ColumnLetter = namedArgument.Value.Value?.ToString();
                            break;
                        case "ConverterType":
                            var converterTypeValue = namedArgument.Value.Value;
                            if (converterTypeValue is INamedTypeSymbol converterTypeSymbol)
                            {
                                propertyInfo.ConverterType = converterTypeSymbol.ToDisplayString();
                            }

                            break;
                    }
                }
            }

            propertyInfo.IsKey = member.GetAttributes()
                .Any(a => a.AttributeClass?.Name == "KeyAttribute");

            entityInfo.Properties.Add(propertyInfo);
        }

        return entityInfo;
    }

    private static bool IsPropertyNullable(IPropertySymbol property)
    {
        // Check for nullable reference types (string?, object?, etc.)
        if (property.Type.NullableAnnotation == NullableAnnotation.Annotated)
            return true;

        // Check for nullable value types (int?, DateTime?, etc.)
        if (property.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return true;

        // Reference types without nullable annotation in nullable context are non-nullable
        if (property.Type.IsReferenceType)
        {
            return property.Type.NullableAnnotation != NullableAnnotation.NotAnnotated;
        }

        // Value types are non-nullable by default
        return false;
    }

    private static void Execute(Compilation _, ImmutableArray<EntityClassInfo?> entities,
        SourceProductionContext context)
    {
        if (entities.IsDefaultOrEmpty)
            return;

        var validEntities = entities.Where(e => e is not null).Cast<EntityClassInfo>().ToList();

        if (!validEntities.Any())
            return;

        var contextSource = GenerateContextImplementation(validEntities);
        context.AddSource("GoogleSheetsContext.Generated.cs", SourceText.From(contextSource, Encoding.UTF8));

        foreach (var entity in validEntities)
        {
            var extensionSource = GenerateEntityExtensions(entity);
            context.AddSource($"{entity.ClassName}Extensions.Generated.cs",
                SourceText.From(extensionSource, Encoding.UTF8));
        }

        var mappingSource = GenerateMappingUtilities(validEntities);
        context.AddSource("GoogleSheetsMappingUtilities.Generated.cs", SourceText.From(mappingSource, Encoding.UTF8));

        var columnUtilitiesSource = GenerateColumnUtilities();
        context.AddSource("ColumnMappingUtilities.Generated.cs", SourceText.From(columnUtilitiesSource, Encoding.UTF8));
    }

    private static string GenerateContextImplementation(List<EntityClassInfo> entities)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Google.Apis.Sheets.v4;");
        sb.AppendLine("using Google.Apis.Sheets.v4.Data;");
        sb.AppendLine("using MyParadeManager.WebApi.Entities.Shared;");
        sb.AppendLine("using MyParadeManager.WebApi.Entities.Tenant;");
        sb.AppendLine("using MyParadeManager.WebApi.GoogleSheets.EntityOperations;");
        sb.AppendLine();
        sb.AppendLine("namespace MyParadeManager.WebApi.GoogleSheets;");
        sb.AppendLine();
        sb.AppendLine("public partial class GoogleSheetsContext");
        sb.AppendLine("{");

        // Generic methods with optional configuration function
        sb.AppendLine(
            "    public async Task<IEnumerable<T>> GetAsync<T>(CancellationToken cancellationToken = default) where T : class, new()");
        sb.AppendLine("    {");
        sb.AppendLine("        return await GetAsync<T>(_ => _, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine(
            "    public async Task<IEnumerable<T>> GetAsync<T>(Func<GoogleSheetsConfiguration, GoogleSheetsConfiguration> configurationFunc, CancellationToken cancellationToken = default) where T : class, new()");
        sb.AppendLine("    {");
        sb.AppendLine("        return typeof(T).Name switch");
        sb.AppendLine("        {");
        foreach (var entity in entities)
        {
            sb.AppendLine(
                $"            \"{entity.ClassName}\" => (IEnumerable<T>)await GetAsync{entity.ClassName}Internal(configurationFunc, cancellationToken),");
        }

        sb.AppendLine(
            "            _ => throw new ArgumentException($\"Entity type {typeof(T).Name} is not supported.\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine(
            "    public async Task<T?> GetByKeyAsync<T>(object key, CancellationToken cancellationToken = default) where T : class, new()");
        sb.AppendLine("    {");
        sb.AppendLine("        return await GetByKeyAsync<T>(key, _ => _, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine(
            "    public async Task<T?> GetByKeyAsync<T>(object key, Func<GoogleSheetsConfiguration, GoogleSheetsConfiguration> configurationFunc, CancellationToken cancellationToken = default) where T : class, new()");
        sb.AppendLine("    {");
        sb.AppendLine("        return typeof(T).Name switch");
        sb.AppendLine("        {");
        foreach (var entity in entities)
        {
            sb.AppendLine(
                $"            \"{entity.ClassName}\" => (T?)(object?)await GetByKeyAsync{entity.ClassName}Internal(key, configurationFunc, cancellationToken),");
        }

        sb.AppendLine(
            "            _ => throw new ArgumentException($\"Entity type {typeof(T).Name} is not supported.\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine(
            "    public Task<T> AddAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, new()");
        sb.AppendLine("    {");
        sb.AppendLine("        return AddAsync(entity, _ => _, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine(
            "    public Task<T> AddAsync<T>(T entity, Func<GoogleSheetsConfiguration, GoogleSheetsConfiguration> configurationFunc, CancellationToken cancellationToken = default) where T : class, new()");
        sb.AppendLine("    {");
        sb.AppendLine("        return entity switch");
        sb.AppendLine("        {");
        foreach (var entity in entities)
        {
            sb.AppendLine(
                $"            {entity.ClassName} {entity.ClassName.ToLowerInvariant()}Entity => (Task<T>)(object)AddAsync{entity.ClassName}Internal({entity.ClassName.ToLowerInvariant()}Entity, configurationFunc, cancellationToken),");
        }

        sb.AppendLine(
            "            _ => throw new ArgumentException($\"Entity type {typeof(T).Name} is not supported.\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine(
            "    public Task<T> UpdateAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, new()");
        sb.AppendLine("    {");
        sb.AppendLine("        return UpdateAsync(entity, _ => _, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine(
            "    public Task<T> UpdateAsync<T>(T entity, Func<GoogleSheetsConfiguration, GoogleSheetsConfiguration> configurationFunc, CancellationToken cancellationToken = default) where T : class, new()");
        sb.AppendLine("    {");
        sb.AppendLine("        return entity switch");
        sb.AppendLine("        {");
        foreach (var entity in entities)
        {
            sb.AppendLine(
                $"            {entity.ClassName} {entity.ClassName.ToLowerInvariant()}Entity => (Task<T>)(object)UpdateAsync{entity.ClassName}Internal({entity.ClassName.ToLowerInvariant()}Entity, configurationFunc, cancellationToken),");
        }

        sb.AppendLine(
            "            _ => throw new ArgumentException($\"Entity type {typeof(T).Name} is not supported.\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine(
            "    public Task DeleteAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, new()");
        sb.AppendLine("    {");
        sb.AppendLine("        return DeleteAsync(entity, _ => _, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine(
            "    public Task DeleteAsync<T>(T entity, Func<GoogleSheetsConfiguration, GoogleSheetsConfiguration> configurationFunc, CancellationToken cancellationToken = default) where T : class, new()");
        sb.AppendLine("    {");
        sb.AppendLine("        return entity switch");
        sb.AppendLine("        {");
        foreach (var entity in entities)
        {
            sb.AppendLine(
                $"            {entity.ClassName} {entity.ClassName.ToLowerInvariant()}Entity => DeleteAsync{entity.ClassName}Internal({entity.ClassName.ToLowerInvariant()}Entity, configurationFunc, cancellationToken),");
        }

        sb.AppendLine(
            "            _ => throw new ArgumentException($\"Entity type {typeof(T).Name} is not supported.\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();

        foreach (var entity in entities)
        {
            GenerateEntityMethods(sb, entity);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void GenerateEntityMethods(StringBuilder sb, EntityClassInfo entity)
    {
        var keyProperty = entity.Properties.FirstOrDefault(p => p.IsKey);
        var maxColumnIndex = CalculateMaxColumnIndex(entity.Properties);
        var maxColumnLetter = maxColumnIndex >= 0 ? IndexToColumnLetter(maxColumnIndex) : "Z";

        // GetAsync method with configuration function
        sb.AppendLine(
            $"    private async Task<IEnumerable<{entity.ClassName}>> GetAsync{entity.ClassName}Internal(Func<GoogleSheetsConfiguration, GoogleSheetsConfiguration> configurationFunc, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        cancellationToken.ThrowIfCancellationRequested();");
        sb.AppendLine();
        sb.AppendLine("        // Apply the configuration function to get the effective configuration");
        sb.AppendLine("        var effectiveConfig = configurationFunc(_configuration);");
        sb.AppendLine();
        sb.AppendLine(
            $"        var sheetId = {(string.IsNullOrEmpty(entity.SheetId) ? "effectiveConfig.DefaultSheetId" : $"\"{entity.SheetId}\"")};");

        var sheetReference = !string.IsNullOrEmpty(entity.SheetName) ? $"{entity.SheetName}!" : "";

        sb.AppendLine($"        var range = \"{sheetReference}A{entity.StartRow}:{maxColumnLetter}\";");
        sb.AppendLine();
        sb.AppendLine("        if (string.IsNullOrEmpty(sheetId))");
        sb.AppendLine(
            "            throw new InvalidOperationException(\"Sheet ID must be provided either globally or per entity.\");");
        sb.AppendLine();
        sb.AppendLine("        var request = _sheetsService.Spreadsheets.Values.Get(sheetId, range);");
        sb.AppendLine("        var response = await request.ExecuteAsync(cancellationToken);");
        sb.AppendLine();
        sb.AppendLine(
            $"        return GoogleSheetsMappingUtilities.MapToEntities{entity.ClassName}(response.Values, {entity.HasHeader.ToString().ToLower()}, {entity.StartRow});");
        sb.AppendLine("    }");
        sb.AppendLine();

        // GetByKeyAsync method with configuration function
        if (keyProperty != null)
        {
            sb.AppendLine(
                $"    private async Task<{entity.ClassName}?> GetByKeyAsync{entity.ClassName}Internal(object key, Func<GoogleSheetsConfiguration, GoogleSheetsConfiguration> configurationFunc, CancellationToken cancellationToken = default)");
            sb.AppendLine("    {");
            sb.AppendLine("        cancellationToken.ThrowIfCancellationRequested();");
            sb.AppendLine();
            sb.AppendLine(
                $"        var entities = await GetAsync{entity.ClassName}Internal(configurationFunc, cancellationToken);");
            sb.AppendLine(
                $"        return entities.FirstOrDefault(e => e.{keyProperty.Name}{(keyProperty.IsNullable ? '?' : string.Empty)}.ToString() == key.ToString());");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine(
                $"    private Task<{entity.ClassName}?> GetByKeyAsync{entity.ClassName}Internal(object key, Func<GoogleSheetsConfiguration, GoogleSheetsConfiguration> configurationFunc, CancellationToken cancellationToken = default)");
            sb.AppendLine("    {");
            sb.AppendLine("        cancellationToken.ThrowIfCancellationRequested();");
            sb.AppendLine(
                $"        throw new NotSupportedException(\"Entity {entity.ClassName} does not have a key property defined.\");");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // AddAsync method with configuration function
        sb.AppendLine(
            $"    private Task<{entity.ClassName}> AddAsync{entity.ClassName}Internal({entity.ClassName} entity, Func<GoogleSheetsConfiguration, GoogleSheetsConfiguration> configurationFunc, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        cancellationToken.ThrowIfCancellationRequested();");
        sb.AppendLine();
        sb.AppendLine("        // Apply the configuration function to get the effective configuration");
        sb.AppendLine("        var effectiveConfig = configurationFunc(_configuration);");
        sb.AppendLine();

        var requiredProperties = entity.Properties.Where(p => !p.IsNullable).ToList();
        if (requiredProperties.Any())
        {
            foreach (var prop in requiredProperties)
            {
                if (prop.Type.Contains("string") && !prop.Type.Contains("?"))
                {
                    sb.AppendLine($"        if (string.IsNullOrEmpty(entity.{prop.Name}))");
                    sb.AppendLine(
                        $"            throw new ArgumentException(\"Property {prop.Name} is required but was null or empty.\");");
                }
                else if (!prop.Type.Contains("?") && !IsValueType(prop.Type))
                {
                    sb.AppendLine($"        if (entity.{prop.Name} == null)");
                    sb.AppendLine(
                        $"            throw new ArgumentException(\"Property {prop.Name} is required but was null.\");");
                }

                sb.AppendLine();
            }
        }

        sb.AppendLine(
            $"        var sheetId = {(string.IsNullOrEmpty(entity.SheetId) ? "effectiveConfig.DefaultSheetId" : $"\"{entity.SheetId}\"")};");

        // Calculate the proper range for appending data, respecting StartRow
        var appendStartRow = entity.HasHeader ? entity.StartRow + 1 : entity.StartRow;
        sb.AppendLine($"        var range = \"{entity.SheetName}!A{appendStartRow}:{maxColumnLetter}\";");

        sb.AppendLine();
        sb.AppendLine("        if (string.IsNullOrEmpty(sheetId))");
        sb.AppendLine(
            "            throw new InvalidOperationException(\"Sheet ID must be provided either globally or per entity.\");");
        sb.AppendLine();
        sb.AppendLine($"        var values = GoogleSheetsMappingUtilities.MapFromEntity{entity.ClassName}(entity);");
        sb.AppendLine(
            $"        AddPendingOperation(new AddEntityOperation<{entity.ClassName}>(entity, sheetId, range, values));");
        sb.AppendLine("        return Task.FromResult(entity);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // UpdateAsync method with configuration function
        sb.AppendLine(
            $"    private Task<{entity.ClassName}> UpdateAsync{entity.ClassName}Internal({entity.ClassName} entity, Func<GoogleSheetsConfiguration, GoogleSheetsConfiguration> configurationFunc, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        cancellationToken.ThrowIfCancellationRequested();");
        sb.AppendLine();
        sb.AppendLine("        // Apply the configuration function to get the effective configuration");
        sb.AppendLine("        var effectiveConfig = configurationFunc(_configuration);");
        sb.AppendLine();

        var propertyColumnInfos = entity.Properties.Select(p => new
        {
            p.Name,
            p.Type,
            p.Order,
            p.ColumnLetter,
            p.ConverterType,
            p.IsNullable,
            p.IsKey
        }).ToList();

        if (keyProperty != null)
        {
            // Validation for required properties
            if (requiredProperties.Any())
            {
                foreach (var prop in requiredProperties)
                {
                    if (prop.Type.Contains("string") && !prop.Type.Contains("?"))
                    {
                        sb.AppendLine($"        if (string.IsNullOrEmpty(entity.{prop.Name}))");
                        sb.AppendLine(
                            $"            throw new ArgumentException(\"Property {prop.Name} is required but was null or empty.\");");
                    }
                    else if (!prop.Type.Contains("?") && !IsValueType(prop.Type))
                    {
                        sb.AppendLine($"        if (entity.{prop.Name} == null)");
                        sb.AppendLine(
                            $"            throw new ArgumentException(\"Property {prop.Name} is required but was null.\");");
                    }

                    sb.AppendLine();
                }
            }

            // Build column mappings to find key column index
            sb.AppendLine("        // Build column mappings to find key column index");
            sb.AppendLine("        var propertyColumnInfos = new List<PropertyColumnInfo>");
            sb.AppendLine("        {");

            foreach (var prop in propertyColumnInfos)
            {
                sb.AppendLine(
                    $"            new PropertyColumnInfo {{ Name = \"{prop.Name}\", Type = \"{prop.Type}\", Order = {prop.Order}, ColumnLetter = {(string.IsNullOrEmpty(prop.ColumnLetter) ? "null" : $"\"{prop.ColumnLetter}\"")}, ConverterType = {(string.IsNullOrEmpty(prop.ConverterType) ? "null" : $"\"{prop.ConverterType}\"")}, IsNullable = {prop.IsNullable.ToString().ToLower()}, IsKey = {prop.IsKey.ToString().ToLower()} }},");
            }

            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine(
                "        var columnMappings = ColumnMappingUtilities.BuildColumnMappings(propertyColumnInfos);");
            sb.AppendLine(
                $"        var keyMapping = columnMappings.FirstOrDefault(m => m.PropertyName == \"{keyProperty.Name}\");");
            sb.AppendLine();
            sb.AppendLine("        if (keyMapping == null)");
            sb.AppendLine(
                $"            throw new InvalidOperationException(\"Key property {keyProperty.Name} not found in column mappings.\");");
            sb.AppendLine();

            sb.AppendLine(
                $"        var sheetId = {(string.IsNullOrEmpty(entity.SheetId) ? "effectiveConfig.DefaultSheetId" : $"\"{entity.SheetId}\"")};");
            sb.AppendLine($"        var sheetName = \"{entity.SheetName}\";");
            sb.AppendLine($"        var keyValue = entity.{keyProperty.Name};");
            sb.AppendLine($"        var keyPropertyName = \"{keyProperty.Name}\";");
            sb.AppendLine("        var keyColumnIndex = keyMapping.ColumnIndex;");
            sb.AppendLine($"        var hasHeader = {entity.HasHeader.ToString().ToLower()};");
            sb.AppendLine($"        var startRow = {entity.StartRow};");
            sb.AppendLine($"        var maxColumnLetter = \"{maxColumnLetter}\";");
            sb.AppendLine();
            sb.AppendLine("        if (string.IsNullOrEmpty(sheetId))");
            sb.AppendLine(
                "            throw new InvalidOperationException(\"Sheet ID must be provided either globally or per entity.\");");
            sb.AppendLine();
            sb.AppendLine("        if (keyValue == null)");
            sb.AppendLine(
                $"            throw new ArgumentException(\"Key property {keyProperty.Name} cannot be null for updates.\");");
            sb.AppendLine();
            sb.AppendLine(
                $"        var values = GoogleSheetsMappingUtilities.MapFromEntity{entity.ClassName}(entity);");
            sb.AppendLine($"        AddPendingOperation(new UpdateEntityOperation<{entity.ClassName}>(");
            sb.AppendLine(
                "            entity, sheetId, sheetName, keyValue, keyPropertyName, keyColumnIndex, hasHeader, startRow, maxColumnLetter, values));");
            sb.AppendLine("        return Task.FromResult(entity);");
        }
        else
        {
            sb.AppendLine(
                $"        throw new NotSupportedException(\"Entity {entity.ClassName} does not have a key property defined for updates.\");");
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        // DeleteAsync method with configuration function
        sb.AppendLine(
            $"    private Task DeleteAsync{entity.ClassName}Internal({entity.ClassName} entity, Func<GoogleSheetsConfiguration, GoogleSheetsConfiguration> configurationFunc, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        cancellationToken.ThrowIfCancellationRequested();");
        sb.AppendLine();
        sb.AppendLine("        // Apply the configuration function to get the effective configuration");
        sb.AppendLine("        var effectiveConfig = configurationFunc(_configuration);");
        sb.AppendLine();

        if (keyProperty != null)
        {
            // Build column mappings to find key column index
            sb.AppendLine("        // Build column mappings to find key column index");
            sb.AppendLine("        var propertyColumnInfos = new List<PropertyColumnInfo>");
            sb.AppendLine("        {");

            foreach (var prop in propertyColumnInfos)
            {
                sb.AppendLine(
                    $"            new PropertyColumnInfo {{ Name = \"{prop.Name}\", Type = \"{prop.Type}\", Order = {prop.Order}, ColumnLetter = {(string.IsNullOrEmpty(prop.ColumnLetter) ? "null" : $"\"{prop.ColumnLetter}\"")}, ConverterType = {(string.IsNullOrEmpty(prop.ConverterType) ? "null" : $"\"{prop.ConverterType}\"")}, IsNullable = {prop.IsNullable.ToString().ToLower()}, IsKey = {prop.IsKey.ToString().ToLower()} }},");
            }

            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine(
                "        var columnMappings = ColumnMappingUtilities.BuildColumnMappings(propertyColumnInfos);");
            sb.AppendLine(
                $"        var keyMapping = columnMappings.FirstOrDefault(m => m.PropertyName == \"{keyProperty.Name}\");");
            sb.AppendLine();
            sb.AppendLine("        if (keyMapping == null)");
            sb.AppendLine(
                $"            throw new InvalidOperationException(\"Key property {keyProperty.Name} not found in column mappings.\");");
            sb.AppendLine();

            sb.AppendLine(
                $"        var sheetId = {(string.IsNullOrEmpty(entity.SheetId) ? "effectiveConfig.DefaultSheetId" : $"\"{entity.SheetId}\"")};");
            sb.AppendLine($"        var sheetName = \"{entity.SheetName}\";");
            sb.AppendLine($"        var keyValue = entity.{keyProperty.Name};");
            sb.AppendLine($"        var keyPropertyName = \"{keyProperty.Name}\";");
            sb.AppendLine("        var keyColumnIndex = keyMapping.ColumnIndex;");
            sb.AppendLine($"        var hasHeader = {entity.HasHeader.ToString().ToLower()};");
            sb.AppendLine($"        var startRow = {entity.StartRow};");
            sb.AppendLine();
            sb.AppendLine("        if (string.IsNullOrEmpty(sheetId))");
            sb.AppendLine(
                "            throw new InvalidOperationException(\"Sheet ID must be provided either globally or per entity.\");");
            sb.AppendLine();
            sb.AppendLine("        if (keyValue == null)");
            sb.AppendLine(
                $"            throw new ArgumentException(\"Key property {keyProperty.Name} cannot be null for deletion.\");");
            sb.AppendLine();
            sb.AppendLine($"        AddPendingOperation(new DeleteEntityOperation<{entity.ClassName}>(");
            sb.AppendLine(
                "            entity, sheetId, sheetName, keyValue, keyPropertyName, keyColumnIndex, hasHeader, startRow));");
            sb.AppendLine("        return Task.CompletedTask;");
        }
        else
        {
            sb.AppendLine(
                $"        throw new NotSupportedException(\"Entity {entity.ClassName} does not have a key property defined for deletion.\");");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static bool IsValueType(string typeName)
    {
        var valueTypes = new HashSet<string>
        {
            "int", "long", "short", "byte", "sbyte", "uint", "ulong", "ushort",
            "float", "double", "decimal", "bool", "char", "DateTime", "DateTimeOffset",
            "TimeSpan", "Guid", "Int32", "Int64", "Int16", "Byte", "SByte",
            "UInt32", "UInt64", "UInt16", "Single", "Double", "Decimal",
            "Boolean", "Char"
        };

        var simpleTypeName = typeName.Split('.').Last().Replace("?", "");
        return valueTypes.Contains(simpleTypeName);
    }

    private static int CalculateMaxColumnIndex(List<PropertyInfo> properties)
    {
        var maxIndex = -1;
        var usedIndices = new HashSet<int>();

        foreach (var prop in properties.Where(p => !string.IsNullOrEmpty(p.ColumnLetter)))
        {
            try
            {
                var index = ColumnLetterToIndex(prop.ColumnLetter!);
                usedIndices.Add(index);
                maxIndex = Math.Max(maxIndex, index);
            }
            catch
            {
                throw new Exception($"Invalid column letter {prop.ColumnLetter}");
            }
        }

        var orderedProps = properties.Where(p => string.IsNullOrEmpty(p.ColumnLetter)).OrderBy(p => p.Order).ToList();
        var nextAvailableIndex = 0;

        foreach (var _ in orderedProps)
        {
            while (usedIndices.Contains(nextAvailableIndex))
            {
                nextAvailableIndex++;
            }

            usedIndices.Add(nextAvailableIndex);
            maxIndex = Math.Max(maxIndex, nextAvailableIndex);
            nextAvailableIndex++;
        }

        return maxIndex;
    }

    private static int ColumnLetterToIndex(string columnLetter)
    {
        if (string.IsNullOrEmpty(columnLetter))
            return 0;

        columnLetter = columnLetter.ToUpperInvariant();
        var result = 0;

        for (var i = 0; i < columnLetter.Length; i++)
        {
            result = result * 26 + (columnLetter[i] - 'A' + 1);
        }

        return result - 1;
    }

    private static string IndexToColumnLetter(int columnIndex)
    {
        if (columnIndex < 0)
            return "A";

        var result = string.Empty;
        columnIndex++;

        while (columnIndex > 0)
        {
            columnIndex--;
            result = (char)('A' + columnIndex % 26) + result;
            columnIndex /= 26;
        }

        return result;
    }

    // Generate entity extensions with support for configuration function
    private static string GenerateEntityExtensions(EntityClassInfo entity)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using MyParadeManager.WebApi.GoogleSheets;");
        sb.AppendLine();
        sb.AppendLine($"namespace {entity.Namespace};");
        sb.AppendLine();
        sb.AppendLine($"public static class {entity.ClassName}Extensions");
        sb.AppendLine("{");

        // GetAsync extension without configuration function
        sb.AppendLine(
            $"    public static async Task<IEnumerable<{entity.ClassName}>> Get{entity.ClassName}Async(this IGoogleSheetsContext context, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine($"        return await context.GetAsync<{entity.ClassName}>(cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // GetAsync extension with configuration function
        sb.AppendLine(
            $"    public static async Task<IEnumerable<{entity.ClassName}>> Get{entity.ClassName}Async(this IGoogleSheetsContext context, Func<GoogleSheetsConfiguration, GoogleSheetsConfiguration> configurationFunc, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine(
            $"        return await context.GetAsync<{entity.ClassName}>(configurationFunc, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();

        var keyProperty = entity.Properties.FirstOrDefault(p => p.IsKey);
        if (keyProperty != null)
        {
            // GetByKeyAsync extension without configuration function
            sb.AppendLine(
                $"    public static async Task<{entity.ClassName}?> Get{entity.ClassName}ByKeyAsync(this IGoogleSheetsContext context, {keyProperty.Type} key, CancellationToken cancellationToken = default)");
            sb.AppendLine("    {");
            sb.AppendLine($"        return await context.GetByKeyAsync<{entity.ClassName}>(key, cancellationToken);");
            sb.AppendLine("    }");
            sb.AppendLine();

            // GetByKeyAsync extension with configuration function
            sb.AppendLine(
                $"    public static async Task<{entity.ClassName}?> Get{entity.ClassName}ByKeyAsync(this IGoogleSheetsContext context, {keyProperty.Type} key, Func<GoogleSheetsConfiguration, GoogleSheetsConfiguration> configurationFunc, CancellationToken cancellationToken = default)");
            sb.AppendLine("    {");
            sb.AppendLine(
                $"        return await context.GetByKeyAsync<{entity.ClassName}>(key, configurationFunc, cancellationToken);");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // AddAsync extension without configuration function
        sb.AppendLine(
            $"    public static async Task<{entity.ClassName}> Add{entity.ClassName}Async(this IGoogleSheetsContext context, {entity.ClassName} entity, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        return await context.AddAsync(entity, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // AddAsync extension with configuration function
        sb.AppendLine(
            $"    public static async Task<{entity.ClassName}> Add{entity.ClassName}Async(this IGoogleSheetsContext context, {entity.ClassName} entity, Func<GoogleSheetsConfiguration, GoogleSheetsConfiguration> configurationFunc, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        return await context.AddAsync(entity, configurationFunc, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // UpdateAsync extension without configuration function
        sb.AppendLine(
            $"    public static async Task<{entity.ClassName}> Update{entity.ClassName}Async(this IGoogleSheetsContext context, {entity.ClassName} entity, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        return await context.UpdateAsync(entity, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // UpdateAsync extension with configuration function
        sb.AppendLine(
            $"    public static async Task<{entity.ClassName}> Update{entity.ClassName}Async(this IGoogleSheetsContext context, {entity.ClassName} entity, Func<GoogleSheetsConfiguration, GoogleSheetsConfiguration> configurationFunc, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        return await context.UpdateAsync(entity, configurationFunc, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // DeleteAsync extension without configuration function
        sb.AppendLine(
            $"    public static async Task Delete{entity.ClassName}Async(this IGoogleSheetsContext context, {entity.ClassName} entity, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        await context.DeleteAsync(entity, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // DeleteAsync extension with configuration function
        sb.AppendLine(
            $"    public static async Task Delete{entity.ClassName}Async(this IGoogleSheetsContext context, {entity.ClassName} entity, Func<GoogleSheetsConfiguration, GoogleSheetsConfiguration> configurationFunc, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        await context.DeleteAsync(entity, configurationFunc, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // Generate mapping utilities (unchanged - no async operations here)
    private static string GenerateMappingUtilities(List<EntityClassInfo> entities)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Globalization;");
        sb.AppendLine("using MyParadeManager.WebApi.Entities.Shared;");
        sb.AppendLine("using MyParadeManager.WebApi.Entities.Tenant;");
        sb.AppendLine("using MyParadeManager.WebApi.GoogleSheets.ValueConverter;");
        sb.AppendLine();
        sb.AppendLine("namespace MyParadeManager.WebApi.GoogleSheets;");
        sb.AppendLine();
        sb.AppendLine("internal static class GoogleSheetsMappingUtilities");
        sb.AppendLine("{");

        GenerateConversionMethods(sb);

        foreach (var entity in entities)
        {
            GenerateEntityMappingMethods(sb, entity);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void GenerateConversionMethods(StringBuilder sb)
    {
        sb.AppendLine("    private static T ConvertValue<T>(object? value, Type? converterType = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value == null)");
        sb.AppendLine("            return default(T)!;");
        sb.AppendLine();
        sb.AppendLine("        var stringValue = value.ToString();");
        sb.AppendLine("        if (string.IsNullOrEmpty(stringValue))");
        sb.AppendLine("            return default(T)!;");
        sb.AppendLine();
        sb.AppendLine("        var targetType = typeof(T);");
        sb.AppendLine("        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;");
        sb.AppendLine();
        sb.AppendLine("        if (converterType != null)");
        sb.AppendLine("        {");
        sb.AppendLine("            var converter = Activator.CreateInstance(converterType) as IValueConverter;");
        sb.AppendLine("            if (converter != null)");
        sb.AppendLine("                return (T)converter.ConvertFromString(stringValue)!;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var registryConverter = ValueConverterRegistry.GetConverter(underlyingType);");
        sb.AppendLine("        if (registryConverter != null)");
        sb.AppendLine("        {");
        sb.AppendLine("            return (T)registryConverter.ConvertFromString(stringValue)!;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return underlyingType.Name switch");
        sb.AppendLine("        {");
        sb.AppendLine("            nameof(String) => (T)(object)stringValue,");
        sb.AppendLine("            nameof(Int32) => (T)(object)ConvertToInt32(stringValue),");
        sb.AppendLine("            nameof(Int64) => (T)(object)ConvertToInt64(stringValue),");
        sb.AppendLine("            nameof(Double) => (T)(object)ConvertToDouble(stringValue),");
        sb.AppendLine("            nameof(Decimal) => (T)(object)ConvertToDecimal(stringValue),");
        sb.AppendLine("            nameof(Boolean) => (T)(object)ConvertToBoolean(stringValue),");
        sb.AppendLine("            nameof(DateTime) => (T)(object)ConvertToDateTime(stringValue),");
        sb.AppendLine("            nameof(DateTimeOffset) => (T)(object)ConvertToDateTimeOffset(stringValue),");
        sb.AppendLine("            nameof(Guid) => (T)(object)ConvertToGuid(stringValue),");
        sb.AppendLine(
            "            _ => throw new NotSupportedException($\"Type {targetType.Name} is not supported for conversion.\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    private static string ToStringValue<T>(T value, Type? converterType = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value == null)");
        sb.AppendLine("            return string.Empty;");
        sb.AppendLine();
        sb.AppendLine("        var targetType = typeof(T);");
        sb.AppendLine("        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;");
        sb.AppendLine();
        sb.AppendLine("        if (converterType != null)");
        sb.AppendLine("        {");
        sb.AppendLine("            var converter = Activator.CreateInstance(converterType) as IValueConverter;");
        sb.AppendLine("            if (converter != null)");
        sb.AppendLine("                return converter.ConvertToString(value);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var registryConverter = ValueConverterRegistry.GetConverter(underlyingType);");
        sb.AppendLine("        if (registryConverter != null)");
        sb.AppendLine("        {");
        sb.AppendLine("            return registryConverter.ConvertToString(value);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return value.ToString() ?? string.Empty;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Add helper conversion methods (unchanged)
        sb.AppendLine("    private static int ConvertToInt32(string value)");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : default;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    private static long ConvertToInt64(string value)");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : default;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    private static double ConvertToDouble(string value)");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result) ? result : default;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    private static decimal ConvertToDecimal(string value)");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        return decimal.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result) ? result : default;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    private static bool ConvertToBoolean(string value)");
        sb.AppendLine("    {");
        sb.AppendLine("        return value.ToLowerInvariant() switch");
        sb.AppendLine("        {");
        sb.AppendLine("            \"true\" or \"1\" or \"yes\" or \"y\" => true,");
        sb.AppendLine("            \"false\" or \"0\" or \"no\" or \"n\" => false,");
        sb.AppendLine("            _ => bool.TryParse(value, out var result) && result");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    private static DateTime ConvertToDateTime(string value)");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))");
        sb.AppendLine("            return result;");
        sb.AppendLine();
        sb.AppendLine(
            "        var formats = new[] { \"yyyy-MM-dd\", \"MM/dd/yyyy\", \"dd/MM/yyyy\", \"yyyy-MM-dd HH:mm:ss\", \"MM/dd/yyyy HH:mm:ss\", \"dd/MM/yyyy HH:mm:ss\" };");
        sb.AppendLine("        foreach (var format in formats)");
        sb.AppendLine("        {");
        sb.AppendLine(
            "            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))");
        sb.AppendLine("                return result;");
        sb.AppendLine("        }");
        sb.AppendLine("        return default;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    private static DateTimeOffset ConvertToDateTimeOffset(string value)");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) ? result : default;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    private static Guid ConvertToGuid(string value)");
        sb.AppendLine("    {");
        sb.AppendLine("        return Guid.TryParse(value, out var result) ? result : default;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void GenerateEntityMappingMethods(StringBuilder sb, EntityClassInfo entity)
    {
        var propertyColumnInfos = entity.Properties.Select(p => new
        {
            p.Name,
            p.Type,
            p.Order,
            p.ColumnLetter,
            p.ConverterType,
            p.IsNullable,
            p.IsKey
        }).ToList();

        // MapToEntities method (unchanged - no async operations)
        sb.AppendLine(
            $"    internal static IEnumerable<{entity.ClassName}> MapToEntities{entity.ClassName}(IList<IList<object>>? values, bool hasHeader, int startRow)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (values == null || values.Count == 0)");
        sb.AppendLine($"            return Enumerable.Empty<{entity.ClassName}>();");
        sb.AppendLine();

        sb.AppendLine("        var propertyColumnInfos = new List<PropertyColumnInfo>");
        sb.AppendLine("        {");
        foreach (var prop in propertyColumnInfos)
        {
            sb.AppendLine(
                $"            new PropertyColumnInfo {{ Name = \"{prop.Name}\", Type = \"{prop.Type}\", Order = {prop.Order}, ColumnLetter = {(string.IsNullOrEmpty(prop.ColumnLetter) ? "null" : $"\"{prop.ColumnLetter}\"")}, ConverterType = {(string.IsNullOrEmpty(prop.ConverterType) ? "null" : $"\"{prop.ConverterType}\"")}, IsNullable = {prop.IsNullable.ToString().ToLower()}, IsKey = {prop.IsKey.ToString().ToLower()} }},");
        }

        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        var columnMappings = ColumnMappingUtilities.BuildColumnMappings(propertyColumnInfos);");
        sb.AppendLine();

        sb.AppendLine($"        var entities = new List<{entity.ClassName}>();");
        sb.AppendLine("        var dataStartRow = hasHeader ? 1 : 0;");
        sb.AppendLine();
        sb.AppendLine("        for (int i = dataStartRow; i < values.Count; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            var row = values[i];");
        sb.AppendLine($"            var entity = new {entity.ClassName}();");
        sb.AppendLine();
        sb.AppendLine("            foreach (var mapping in columnMappings)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (row.Count > mapping.ColumnIndex && row[mapping.ColumnIndex] != null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var value = row[mapping.ColumnIndex];");
        sb.AppendLine(
            "                    var converterType = !string.IsNullOrEmpty(mapping.ConverterType) ? Type.GetType(mapping.ConverterType) : null;");
        sb.AppendLine();
        sb.AppendLine("                    switch (mapping.PropertyName)");
        sb.AppendLine("                    {");

        foreach (var prop in propertyColumnInfos)
        {
            sb.AppendLine($"                        case \"{prop.Name}\":");
            sb.AppendLine(
                $"                            entity.{prop.Name} = ConvertValue<{prop.Type}>(value, converterType);");
            sb.AppendLine("                            break;");
        }

        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            entities.Add(entity);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return entities;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // MapFromEntity method (unchanged - no async operations)
        sb.AppendLine($"    internal static List<object> MapFromEntity{entity.ClassName}({entity.ClassName} entity)");
        sb.AppendLine("    {");
        sb.AppendLine("        var propertyColumnInfos = new List<PropertyColumnInfo>");
        sb.AppendLine("        {");
        foreach (var prop in propertyColumnInfos)
        {
            sb.AppendLine(
                $"            new PropertyColumnInfo {{ Name = \"{prop.Name}\", Type = \"{prop.Type}\", Order = {prop.Order}, ColumnLetter = {(string.IsNullOrEmpty(prop.ColumnLetter) ? "null" : $"\"{prop.ColumnLetter}\"")}, ConverterType = {(string.IsNullOrEmpty(prop.ConverterType) ? "null" : $"\"{prop.ConverterType}\"")}, IsNullable = {prop.IsNullable.ToString().ToLower()}, IsKey = {prop.IsKey.ToString().ToLower()} }},");
        }

        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        var columnMappings = ColumnMappingUtilities.BuildColumnMappings(propertyColumnInfos);");
        sb.AppendLine(
            "        var maxColumnIndex = columnMappings.Any() ? columnMappings.Max(m => m.ColumnIndex) : 0;");
        sb.AppendLine("        var values = new object[maxColumnIndex + 1];");
        sb.AppendLine();
        sb.AppendLine("        for (int i = 0; i < values.Length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            values[i] = string.Empty;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        foreach (var mapping in columnMappings)");
        sb.AppendLine("        {");
        sb.AppendLine(
            "            var converterType = !string.IsNullOrEmpty(mapping.ConverterType) ? Type.GetType(mapping.ConverterType) : null;");
        sb.AppendLine();
        sb.AppendLine("            switch (mapping.PropertyName)");
        sb.AppendLine("            {");

        foreach (var prop in propertyColumnInfos)
        {
            sb.AppendLine($"                case \"{prop.Name}\":");
            sb.AppendLine(
                $"                    values[mapping.ColumnIndex] = ToStringValue(entity.{prop.Name}, converterType);");
            sb.AppendLine("                    break;");
        }

        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return values.ToList();");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static string GenerateColumnUtilities()
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine();
        sb.AppendLine("namespace MyParadeManager.WebApi.GoogleSheets;");
        sb.AppendLine();
        sb.AppendLine("internal static class ColumnMappingUtilities");
        sb.AppendLine("{");
        sb.AppendLine("    public static int ColumnLetterToIndex(string columnLetter)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (string.IsNullOrEmpty(columnLetter))");
        sb.AppendLine(
            "            throw new ArgumentException(\"Column letter cannot be null or empty.\", nameof(columnLetter));");
        sb.AppendLine();
        sb.AppendLine("        columnLetter = columnLetter.ToUpperInvariant();");
        sb.AppendLine("        int result = 0;");
        sb.AppendLine("        ");
        sb.AppendLine("        for (int i = 0; i < columnLetter.Length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            result = result * 26 + (columnLetter[i] - 'A' + 1);");
        sb.AppendLine("        }");
        sb.AppendLine("        ");
        sb.AppendLine("        return result - 1;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public static string IndexToColumnLetter(int columnIndex)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (columnIndex < 0)");
        sb.AppendLine(
            "            throw new ArgumentException(\"Column index must be non-negative.\", nameof(columnIndex));");
        sb.AppendLine();
        sb.AppendLine("        string result = string.Empty;");
        sb.AppendLine("        columnIndex++;");
        sb.AppendLine("        ");
        sb.AppendLine("        while (columnIndex > 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            columnIndex--;");
        sb.AppendLine("            result = (char)('A' + columnIndex % 26) + result;");
        sb.AppendLine("            columnIndex /= 26;");
        sb.AppendLine("        }");
        sb.AppendLine("        ");
        sb.AppendLine("        return result;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine(
            "    public static List<ColumnMapping> BuildColumnMappings(IEnumerable<PropertyColumnInfo> properties)");
        sb.AppendLine("    {");
        sb.AppendLine("        var mappings = new List<ColumnMapping>();");
        sb.AppendLine("        var propertiesList = properties.ToList();");
        sb.AppendLine();
        sb.AppendLine("        var explicitColumnProperties = propertiesList");
        sb.AppendLine("            .Where(p => !string.IsNullOrEmpty(p.ColumnLetter))");
        sb.AppendLine("            .ToList();");
        sb.AppendLine();
        sb.AppendLine("        foreach (var prop in explicitColumnProperties)");
        sb.AppendLine("        {");
        sb.AppendLine("            var columnIndex = ColumnLetterToIndex(prop.ColumnLetter!);");
        sb.AppendLine("            mappings.Add(new ColumnMapping");
        sb.AppendLine("            {");
        sb.AppendLine("                PropertyName = prop.Name,");
        sb.AppendLine("                PropertyType = prop.Type,");
        sb.AppendLine("                ColumnIndex = columnIndex,");
        sb.AppendLine("                ColumnLetter = prop.ColumnLetter!,");
        sb.AppendLine("                ConverterType = prop.ConverterType,");
        sb.AppendLine("                IsNullable = prop.IsNullable,");
        sb.AppendLine("                IsKey = prop.IsKey");
        sb.AppendLine("            });");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var orderedProperties = propertiesList");
        sb.AppendLine("            .Where(p => string.IsNullOrEmpty(p.ColumnLetter))");
        sb.AppendLine("            .OrderBy(p => p.Order)");
        sb.AppendLine("            .ToList();");
        sb.AppendLine();
        sb.AppendLine("        var usedIndices = mappings.Select(m => m.ColumnIndex).ToHashSet();");
        sb.AppendLine("        int nextAvailableIndex = 0;");
        sb.AppendLine();
        sb.AppendLine("        foreach (var prop in orderedProperties)");
        sb.AppendLine("        {");
        sb.AppendLine("            while (usedIndices.Contains(nextAvailableIndex))");
        sb.AppendLine("            {");
        sb.AppendLine("                nextAvailableIndex++;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            mappings.Add(new ColumnMapping");
        sb.AppendLine("            {");
        sb.AppendLine("                PropertyName = prop.Name,");
        sb.AppendLine("                PropertyType = prop.Type,");
        sb.AppendLine("                ColumnIndex = nextAvailableIndex,");
        sb.AppendLine("                ColumnLetter = IndexToColumnLetter(nextAvailableIndex),");
        sb.AppendLine("                ConverterType = prop.ConverterType,");
        sb.AppendLine("                IsNullable = prop.IsNullable,");
        sb.AppendLine("                IsKey = prop.IsKey");
        sb.AppendLine("            });");
        sb.AppendLine();
        sb.AppendLine("            usedIndices.Add(nextAvailableIndex);");
        sb.AppendLine("            nextAvailableIndex++;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return mappings.OrderBy(m => m.ColumnIndex).ToList();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("internal class PropertyColumnInfo");
        sb.AppendLine("{");
        sb.AppendLine("    public string Name { get; set; } = \"\";");
        sb.AppendLine("    public string Type { get; set; } = \"\";");
        sb.AppendLine("    public int Order { get; set; }");
        sb.AppendLine("    public string? ColumnLetter { get; set; }");
        sb.AppendLine("    public string? ConverterType { get; set; }");
        sb.AppendLine("    public bool IsNullable { get; set; }");
        sb.AppendLine("    public bool IsKey { get; set; }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("internal class ColumnMapping");
        sb.AppendLine("{");
        sb.AppendLine("    public string PropertyName { get; set; } = \"\";");
        sb.AppendLine("    public string PropertyType { get; set; } = \"\";");
        sb.AppendLine("    public int ColumnIndex { get; set; }");
        sb.AppendLine("    public string ColumnLetter { get; set; } = \"\";");
        sb.AppendLine("    public string? ConverterType { get; set; }");
        sb.AppendLine("    public bool IsNullable { get; set; }");
        sb.AppendLine("    public bool IsKey { get; set; }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private class EntityClassInfo
    {
        public string ClassName { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Namespace { get; set; } = "";
        public string? SheetId { get; set; }
        public string? SheetName { get; set; }
        public bool HasHeader { get; set; } = true;
        public int StartRow { get; set; } = 1;
        public List<PropertyInfo> Properties { get; set; } = [];
    }

    private class PropertyInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string? ColumnName { get; set; }
        public int Order { get; set; }
        public string? ColumnLetter { get; set; }
        public bool IsKey { get; set; }
        public bool IsNullable { get; set; }
        public string? ConverterType { get; set; }
    }
}