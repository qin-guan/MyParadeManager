using MyParadeManager.WebApi.Entities;
using MyParadeManager.WebApi.Entities.Shared;
using MyParadeManager.WebApi.GoogleSheets;
using TelegramBotBase.Base;
using TelegramBotBase.DependencyInjection;
using TelegramBotBase.Enums;
using TelegramBotBase.Form;

namespace MyParadeManager.WebApi.Forms;

public class StartForm : AutoCleanForm
{
    private readonly IServiceProvider _serviceProvider;

    public StartForm(IServiceProvider serviceProvider)
    {
        DeleteMode = EDeleteMode.OnLeavingForm;
        _serviceProvider = serviceProvider;

        Init += async (sender, args) => { await Device.Send("Welcome to My Parade Manager!"); };
    }

    public override async Task Load(MessageResult message)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IGoogleSheetsContext>();

        var user = await ctx.GetUserByKeyAsync(message.DeviceId);

        if (user is null)
        {
            await Device.Send("""
                              I can help you to

                              - Manage offs
                              - Keep track of parade state
                              """);

            // Skip render
            await this.NavigateTo<JoinUnitForm>();
        }
    }

    public override async Task Render(MessageResult message)
    {
        var form = new ButtonForm();

        form.AddButtonRow(new ButtonBase("My Teams", new CallbackData("a", "my-teams")));
        form.AddButtonRow(new ButtonBase("Create Team", new CallbackData("a", "create-team")));
        form.AddButtonRow(new ButtonBase("Join Team", new CallbackData("a", "join-team")));

        await Device.Send("What do you want to do?", form);
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
            case "my-teams":
            {
                await this.NavigateTo<ListUnitsForm>();
                break;
            }
            case "create-team":
            {
                await this.NavigateTo<CreateUnitForm>();
                break;
            }
            case "join-team":
            {
                await this.NavigateTo<JoinUnitForm>();
                break;
            }
        }
    }
}