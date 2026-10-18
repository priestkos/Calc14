using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;

namespace CalculatorXIV;

public sealed class CalculatorXIVPlugin : IDalamudPlugin
{
    [PluginService] private static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] private static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] private static IPluginLog Log { get; set; } = null!;

    private const string CommandName = "/calc";
    private readonly WindowSystem windowSystem;
    private readonly CalculatorWindow calculatorWindow;

    public CalculatorXIVPlugin()
    {
        calculatorWindow = new CalculatorWindow();
        windowSystem = new WindowSystem("CalculatorXIV");
        windowSystem.AddWindow(calculatorWindow);

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += calculatorWindow.Toggle;

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the calculator. Usage: /calc [expression] to evaluate inline.",
        });

        Log.Debug("CalculatorXIV loaded");
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
        PluginInterface.UiBuilder.OpenMainUi -= calculatorWindow.Toggle;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        calculatorWindow.Dispose();
        windowSystem.RemoveAllWindows();
    }

    private void OnCommand(string command, string args)
    {
        args = args.Trim();
        if (args.Length > 0)
        {
            var result = CalculatorState.Evaluate(args);
            if (result is not null)
            {
                // Invariant formatting so history clicks parse via TrySetEntry.
                var formatted = CalculatorState.FormatResult(result.Value);
                Log.Info($"{args} = {formatted}");
                calculatorWindow.AddHistory($"{args} = {formatted}");
                return;
            }

            Log.Error($"Could not evaluate: {args}");
            return;
        }

        calculatorWindow.Toggle();
    }
}
