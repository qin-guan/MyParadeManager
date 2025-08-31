using MyParadeManager.WebApi.Entities;
using MyParadeManager.WebApi.Entities.Shared;
using MyParadeManager.WebApi.Forms.Unit;
using MyParadeManager.WebApi.GoogleSheets;
using TelegramBotBase.Base;
using TelegramBotBase.DependencyInjection;
using TelegramBotBase.Enums;
using TelegramBotBase.Form;

namespace MyParadeManager.WebApi.Forms;

public class StartForm : AutoCleanForm
{
    private User? User { get; set; }
    private readonly IServiceProvider _serviceProvider;

    public StartForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task Load(MessageResult message)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IGoogleSheetsContext>();

        User = await ctx.GetUserByKeyAsync(message.DeviceId);
    }

    public override async Task Render(MessageResult message)
    {
        if (User is null)
        {
            await Device.Send("Welcome to My Parade Manager!");
            
            var form = new ButtonForm();
            form.AddButtonRow(new ButtonBase("Get started", new CallbackData("a", "join-team")));

            await Device.Send(
                """
                I can help you to

                - Manage offs
                - Keep track of parade state
                """,
                form
            );
        }
        else
        {
            await Device.Send($"Welcome, {User.Name}!");
            
            var form = new ButtonForm();
            form.AddButtonRow(new ButtonBase("View parade state", new CallbackData("a", "view-parade-state")));
            form.AddButtonRow(new ButtonBase("Unit settings", new CallbackData("a", "unit-settings")));
            
            await Device.Send("What do you want to do today?", form);
        }
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
            case "join-team":
            {
                await this.NavigateTo<JoinUnitForm>();
                break;
            }
            case "view-parade-state":
            {
                await this.NavigateTo<UnitParadeStateForm>();
                break;
            }
            case "unit-settings":
            {
                await this.NavigateTo<UnitSettingsForm>();
                break;
            }
        }
    }
}