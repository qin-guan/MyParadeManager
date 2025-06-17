using Google.Apis.Sheets.v4;
using MyParadeManager.WebApi.Entities;
using MyParadeManager.WebApi.GoogleSheets;
using TelegramBotBase.Base;
using TelegramBotBase.DependencyInjection;
using TelegramBotBase.Form;

namespace MyParadeManager.WebApi.Forms;

public class CreateTeamForm : AutoCleanForm
{
    private readonly IServiceProvider _serviceProvider;

    public string? Name { get; set; }
    public string? SpreadsheetId { get; set; }

    public CreateTeamForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task Load(MessageResult message)
    {
        if (message.MessageText.Trim() == "")
        {
            return;
        }

        if (Name is null)
        {
            if (message.MessageText.Trim() is { Length: < 3 })
            {
                await Device.Send("Team name should be at least 3 characters. Please try again");
                return;
            }

            Name = message.MessageText.Trim();

            return;
        }

        if (SpreadsheetId is null)
        {
            if (!message.MessageText.Trim().StartsWith("https://docs.google.com/spreadsheets/d/"))
            {
                await Device.Send("Please paste a link from Google Sheets. Try again.");
                return;
            }

            var uri = new Uri(message.MessageText);

            var id = uri.AbsolutePath.Split("/").Skip(3).First();
            
            await using var scope = _serviceProvider.CreateAsyncScope();
            var sheetsService = scope.ServiceProvider.GetRequiredService<SheetsService>();

            try
            {
                await sheetsService.Spreadsheets.Get(id).ExecuteAsync();
                SpreadsheetId = id;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await Device.Send("Please ensure file is shared. Try again.");
            }
        }
    }

    public override async Task Action(MessageResult message)
    {
        var call = message.GetData<CallbackData>();

        if (call is null)
        {
            return;
        }

        switch (call.Value)
        {
            case "confirm":
            {
                await using var scope = _serviceProvider.CreateAsyncScope();
                var ctx = scope.ServiceProvider.GetRequiredService<IGoogleSheetsContext>();

                var id = Guid.NewGuid();
                
                await ctx.AddTeamAsync(new Team
                {
                    Id = id,
                    Name = Name,
                    InviteCode = Guid.NewGuid().ToString(),
                    SpreadsheetId = SpreadsheetId
                });

                await ctx.AddUserTeamAsync(new UserTeam
                {
                    Id = Guid.NewGuid(),
                    UserId = message.Message.Chat.Id,
                    TeamId = id,
                });

                await ctx.SaveChangesAsync();

                await Device.Send("Your new team has been created!");

                await message.ConfirmAction();

                await this.NavigateTo<TeamOverviewForm>(id);

                break;
            }
            case "back":
                await this.NavigateTo<StartForm>();
                break;
        }
    }

    public override async Task Render(MessageResult message)
    {
        var bf = new ButtonForm();
        bf.AddButtonRow(new ButtonBase("Cancel", new CallbackData("a", "back")));

        if (Name is null)
        {
            await Device.Send("What should the team name be?", bf);
            return;
        }

        if (SpreadsheetId == null)
        {
            await Device.Send("Please provide a Google Sheets spreadsheet link:", bf);
            return;
        }

        bf.AddButtonRow(new ButtonBase("Confirm", new CallbackData("a", "confirm")));

        await Device.Send($"You entered: {Name} {SpreadsheetId}. Confirm?", bf);
    }
}