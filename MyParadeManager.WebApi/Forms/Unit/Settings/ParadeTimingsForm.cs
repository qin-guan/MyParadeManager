using System.Text.Json;
using MyParadeManager.WebApi.Entities.Shared;
using MyParadeManager.WebApi.GoogleSheets;
using TelegramBotBase.Base;
using TelegramBotBase.Controls.Hybrid;
using TelegramBotBase.DataSources;
using TelegramBotBase.DependencyInjection;
using TelegramBotBase.Enums;
using TelegramBotBase.Form;

namespace MyParadeManager.WebApi.Forms.Unit.Settings;

public class ParadeTimingsForm : AutoCleanForm
{
    private readonly IServiceProvider _serviceProvider;
    private Entities.Shared.Unit? Unit { get; set; }
    private CheckedButtonList _checkedButtonList;

    public ParadeTimingsForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        Init += async (_, args) =>
        {
            _checkedButtonList = new CheckedButtonList
            {
                KeyboardType = EKeyboardType.InlineKeyBoard,
                EnablePaging = true,
                HeadLayoutButtonRow = new List<ButtonBase> { new("Back", "back") },
                SubHeadLayoutButtonRow = new List<ButtonBase> { new("No checked items", "$") }
            };

            var bf = new ButtonForm();

            for (var i = 0; i < 30; i++)
            {
                bf.AddButtonRow($"{i + 1}. Item", i.ToString());
            }

            _checkedButtonList.DataSource = new ButtonFormDataSource(bf);

            _checkedButtonList.ButtonClicked += async (sender, e) =>
            {
                if (e.Button == null)
                {
                    return;
                }

                switch (e.Button.Value)
                {
                    case "back":
                        await this.NavigateTo<StartForm>();
                        break;
                    
                    default:
                        await Device.Send($"Button clicked with Text: {e.Button.Text} and Value {e.Button.Value}");
                        break;
                }
            };
            _checkedButtonList.CheckedChanged += async (sender, eventArgs) =>
            {
                _checkedButtonList.SubHeadLayoutButtonRow = new List<ButtonBase>
                    { new($"{_checkedButtonList.CheckedItems.Count} checked items", "$") };
            };

            AddControl(_checkedButtonList);
        };
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