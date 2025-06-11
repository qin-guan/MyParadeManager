using MyParadeManager.WebApi.Entities;
using MyParadeManager.WebApi.GoogleSheets;
using Telegram.Bot.Types.Enums;
using TelegramBotBase.Base;
using TelegramBotBase.DependencyInjection;
using TelegramBotBase.Form;

namespace MyParadeManager.WebApi.Forms;

public class InviteCodeForm(IServiceProvider sp) : FormBase
{
    public string? InviteCode { get; set; }
    public bool InviteCodeAccepted { get; set; }

    public override async Task Load(MessageResult message)
    {
        if (message is not { MessageType: MessageType.Text }) return;
        if (string.IsNullOrWhiteSpace(message.MessageText)) return;
        if (message.MessageText.StartsWith('/')) return;

        InviteCode = message.MessageText.Trim();

        if (InviteCode != "nice")
        {
            InviteCodeAccepted = false;
            return;
        }

        InviteCodeAccepted = true;

        await using var scope = sp.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IGoogleSheetsContext>();
        await userRepository.AddUserAsync(new User
        {
            Id = message.DeviceId,
            Name = "Test",
            UserType = new UserType("Commander")
        });

        await userRepository.SaveChangesAsync();

        await Device.Send($"✅ Invite code {InviteCode} accepted! You're all set.");
        await this.NavigateTo<StartForm>();
    }

    public override async Task Render(MessageResult message)
    {
        if (InviteCode is null)
        {
            await Device.Send($"Enter your invite code:");
        }

        else
        {
            if (InviteCodeAccepted == false)
            {
                await Device.Send("Failed, try again");
            }
        }
    }
}