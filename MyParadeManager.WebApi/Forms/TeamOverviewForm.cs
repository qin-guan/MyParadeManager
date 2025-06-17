using MyParadeManager.WebApi.Entities;
using MyParadeManager.WebApi.GoogleSheets;
using TelegramBotBase.Base;
using TelegramBotBase.DependencyInjection;
using TelegramBotBase.Form;

namespace MyParadeManager.WebApi.Forms;

public class TeamOverviewForm : AutoCleanForm
{
    private readonly IServiceProvider _serviceProvider;
    public Team? Team { get; set; }

    public TeamOverviewForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        Init += async (_, args) =>
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<IGoogleSheetsContext>();

            var id = Guid.Parse(args.Args[0].ToString() ?? throw new InvalidOperationException());
            Team = await ctx.GetTeamByKeyAsync(id);
        };
    }

    public override async Task Load(MessageResult message)
    {
        if (Team is null)
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

        await Device.Send($"Team {Team.Name} {Team.Id}", form);
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
                await this.NavigateTo<ListTeamsForm>();
                break;
            }
        }
    }
}