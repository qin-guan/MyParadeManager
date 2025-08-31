using MyParadeManager.WebApi.Entities;
using MyParadeManager.WebApi.Entities.Shared;
using MyParadeManager.WebApi.Entities.Tenant;
using MyParadeManager.WebApi.GoogleSheets;
using TelegramBotBase.Base;
using TelegramBotBase.DependencyInjection;
using TelegramBotBase.Form;

namespace MyParadeManager.WebApi.Forms.Unit;

public class UnitParadeStateForm : AutoCleanForm
{
    private readonly IServiceProvider _serviceProvider;
    public Entities.Shared.Unit? Unit { get; set; }

    public UnitParadeStateForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task Load(MessageResult message)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IGoogleSheetsContext>();
        var user = await ctx.GetUserByKeyAsync(message.Message.Chat.Id);

        Unit = await ctx.GetUnitByKeyAsync(user.Unit);
        if (Unit is null)
        {
            await Device.Send("Team not found, returning to home...");
            await this.NavigateTo<StartForm>();
        }
    }

    public override async Task Render(MessageResult message)
    {
        var form = new ButtonForm();

        form.AddButtonRow(new ButtonBase("Back", new CallbackData("a", "back")));
        form.AddButtonRow(new ButtonBase("Parade State", new CallbackData("a", "parade-state")));
        form.AddButtonRow(new ButtonBase("Manage Personnel", new CallbackData("a", "personnel")));

        await Device.Send($"Team {Unit.Name} {Unit.Code}", form);
    }

    public override async Task Action(MessageResult message)
    {
        var call = message.GetData<CallbackData>();

        await message.ConfirmAction();

        if (call is null)
        {
            return;
        }

        message.Handled = true;

        switch (call.Value)
        {
            case "back":
            {
                message.Handled = true;
                await this.NavigateTo<StartForm>();
                break;
            }
        }
    }
}