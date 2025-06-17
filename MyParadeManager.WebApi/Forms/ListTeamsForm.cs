using System.Collections.Immutable;
using MyParadeManager.WebApi.Entities;
using MyParadeManager.WebApi.GoogleSheets;
using TelegramBotBase.Base;
using TelegramBotBase.DependencyInjection;
using TelegramBotBase.Form;

namespace MyParadeManager.WebApi.Forms;

public class ListTeamsForm : AutoCleanForm
{
    private readonly IServiceProvider _serviceProvider;

    public ListTeamsForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task Render(MessageResult message)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IGoogleSheetsContext>();
        
        var form = new ButtonForm();
        form.AddButtonRow("Back", new CallbackData("a", "back"));

        var teams = (await ctx.GetTeamAsync()).ToImmutableList();
        var userTeams = (await ctx.GetUserTeamAsync())
            .Where(ut => ut.UserId == message.Message.Chat.Id)
            .Select(ut => teams.Single(t => t.Id == ut.TeamId));
        
        foreach (var team in userTeams)
        {
            form.AddButtonRow(team.Name, new CallbackData("a", team.Id.ToString()));
        }

        if (teams.Count == 0)
        {
            form.AddButtonRow("No teams. Create one?", new CallbackData("a", "create-team"));
        }
        
        await Device.Send("Your teams", form);
    }

    public override async Task Action(MessageResult message)
    {
        var data = message.GetData<CallbackData>();
        if (data is null)
        {
            return;
        }

        switch (data.Value)
        {
            case "back":
            {
                await this.NavigateTo<StartForm>();
                break;
            }
            case "create-team":
            {
                await this.NavigateTo<CreateTeamForm>();
                break;
            }
            default:
                await this.NavigateTo<TeamOverviewForm>(data.Value);
                break;
        }
    }
}