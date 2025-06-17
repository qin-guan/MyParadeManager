using MyParadeManager.WebApi.Entities;
using MyParadeManager.WebApi.GoogleSheets;
using TelegramBotBase.Base;
using TelegramBotBase.DependencyInjection;
using TelegramBotBase.Form;

namespace MyParadeManager.WebApi.Forms;

public class JoinTeamForm(IServiceProvider sp) : FormBase
{
    public string? InviteCode { get; set; }

    public override async Task Load(MessageResult message)
    {
        if (message.MessageText.Trim() == "")
        {
            return;
        }

        await using var scope = sp.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IGoogleSheetsContext>();
        
        InviteCode = message.MessageText.Trim();

        var team = (await ctx.GetTeamAsync()).FirstOrDefault(t => t.InviteCode == InviteCode);
        if (team is null)
        {
            InviteCode = null;
            await Device.Send("Invalid invite code, try again!");
            return;
        }

        var users = await ctx.GetUserTeamAsync();
        if (users.Any(ut => ut.TeamId == team.Id && ut.UserId == message.Message.Chat.Id))
        {
            InviteCode = null;
            await Device.Send($"You're already in team {team.Name}! Bringing you there now!");
            await this.NavigateTo<TeamOverviewForm>(team.Id);
            return;
        }
        
        await ctx.AddUserTeamAsync(new UserTeam
        {
            Id = Guid.NewGuid(),
            UserId = message.Message.Chat.Id,
            TeamId = team.Id,
            Role = new UserTeamRole("Owner")
        });

        await ctx.SaveChangesAsync();

        await Device.Send($"✅ Invite code {InviteCode} accepted! You're all set.");
        await this.NavigateTo<StartForm>();
    }

    public override async Task Render(MessageResult message)
    {
        var form = new ButtonForm();
        
        form.AddButtonRow("Back", new CallbackData("a", "back"));
        
        if (InviteCode is null)
        {
            await Device.Send($"Enter your invite code to join a team:", form);
        }
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
        }
    }
}