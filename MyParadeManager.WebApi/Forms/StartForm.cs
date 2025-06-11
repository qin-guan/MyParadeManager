using MyParadeManager.WebApi.Entities;
using MyParadeManager.WebApi.GoogleSheets;
using TelegramBotBase.Base;
using TelegramBotBase.DependencyInjection;
using TelegramBotBase.Form;

namespace MyParadeManager.WebApi.Forms;

public class StartForm(IServiceProvider sp) : AutoCleanForm
{
    public override async Task Load(MessageResult message)
    {
        await using var scope = sp.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IGoogleSheetsContext>();

        var user = await userRepository.GetUserByKeyAsync(message.DeviceId);

        if (user is null)
        {
            await Device.Send("Welcome to My Parade Manager!");
            await Device.Send("""
                              I can help you to

                              - Manage offs
                              - Keep track of parade state
                              """);
            await this.NavigateTo<InviteCodeForm>();
            return;
        }

        await Device.Send("Select menu options:");
    }
}