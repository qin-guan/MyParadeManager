using MyParadeManager.WebApi.Entities;
using MyParadeManager.WebApi.Entities.Shared;
using MyParadeManager.WebApi.Entities.Tenant;
using MyParadeManager.WebApi.Forms.Unit;
using MyParadeManager.WebApi.GoogleSheets;
using TelegramBotBase.Base;
using TelegramBotBase.DependencyInjection;
using TelegramBotBase.Form;

namespace MyParadeManager.WebApi.Forms;

public class JoinUnitForm(IServiceProvider sp) : FormBase
{
    public Entities.Shared.User? User { get; set; }
    public Entities.Shared.Unit? Unit { get; set; }
    public SubUnit? SubUnit { get; set; }

    public override async Task Load(MessageResult message)
    {
        if (message.MessageText.Trim() is "" or "/start")
        {
            return;
        }

        await using var scope = sp.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IGoogleSheetsContext>();

        User = (await ctx.GetUserByKeyAsync(message.Message.Chat.Id));
        if (User is null)
        {
            var name = message.MessageText.Trim();
            User = await ctx.AddUserAsync(new User
            {
                Id = message.Message.Chat.Id,
                Name = name
            });
            await ctx.SaveChangesAsync();
            return;
        }

        if (Unit is null)
        {
            Unit = (await ctx.GetUnitAsync()).SingleOrDefault(u => u.Code == message.MessageText.Trim());
            if (Unit is null)
            {
                await Device.Send("Invalid invite code, try again!");
                return;
            }

            User.Unit = Unit.Code;
            await ctx.UpdateUserAsync(User);
            await ctx.SaveChangesAsync();
            return;
        }

        if (SubUnit is null)
        {
            var inviteCode = message.MessageText.Trim();

            SubUnit = (await ctx.GetSubUnitAsync(o => o with { DefaultSheetId = Unit.SpreadsheetId }))
                .SingleOrDefault(su => su.InviteCode == inviteCode);
            if (SubUnit is null)
            {
                await Device.Send("Invalid invite code");
                return;
            }

            await ctx.AddSubUnitUsersAsync(
                new SubUnitUsers
                {
                    Id = Guid.NewGuid(),
                    SubUnitId = SubUnit.Id,
                    UserId = message.Message.Chat.Id
                },
                o => o with { DefaultSheetId = Unit.SpreadsheetId }
            );
            await ctx.SaveChangesAsync();
        }

        await Device.Send($"✅ Invite code accepted! You're all set.");
        await this.NavigateTo<StartForm>();
    }

    public override async Task Render(MessageResult message)
    {
        var form = new ButtonForm();

        form.AddButtonRow("Back", new CallbackData("a", "back"));

        if (User is null)
        {
            await Device.Send("What is your preferred name?");
            return;
        }

        if (Unit is null)
        {
            await Device.Send("What is your unit code?");
            return;
        }

        if (SubUnit is null)
        {
            await Device.Send($"What is your teams invite code?", form);
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