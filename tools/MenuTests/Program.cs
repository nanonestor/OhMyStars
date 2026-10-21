using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using HarmonyLib;
using OhMyStars;

static void Check(bool condition, string message) {
    if(!condition) throw new InvalidOperationException(message);
}

OhMyStarsEntryPoint.OnFullyLoaded();
var hook = AccessTools.Method(typeof(KSA.Program), nameof(KSA.Program.DrawProgramMenusHook));
Check(Harmony.GetPatchInfo(hook)?.Postfixes.Any(patch =>
    patch.owner == "nanonestor.ohmystars" &&
    patch.PatchMethod.Name == "DrawProgramMenu") == true, "Fallback must be installed on the game hook.");

var game = new KSA.Program();
game.DrawProgramMenusHook();
Check(ImGui.Items.SequenceEqual(new[] { "Oh My Stars", "Stars Editor" }), "Exact fallback labels.");
Check(!OhMyStarsWindow.IsOpen && !StarsEditWindow.IsOpen, "No click leaves windows closed.");

ImGui.Items.Clear();
ImGui.Clicked = "Oh My Stars";
game.DrawProgramMenusHook();
Check(OhMyStarsWindow.IsOpen && !StarsEditWindow.IsOpen, "Main entry opens only the main window.");
game.DrawProgramMenusHook();
Check(OhMyStarsWindow.IsOpen, "Clicking the main entry again must not close the window.");

OhMyStarsWindow.IsOpen = false;
ImGui.Clicked = "Stars Editor";
game.DrawProgramMenusHook();
Check(StarsEditWindow.IsOpen && !OhMyStarsWindow.IsOpen, "Editor entry opens only the editor.");
game.DrawProgramMenusHook();
Check(StarsEditWindow.IsOpen, "Clicking the editor entry again must not close the window.");

ImGui.Items.Clear();
ImGui.Clicked = null;
_ = typeof(ModMenu.ModMenuEntryAttribute).Assembly.GetName();
game.DrawProgramMenusHook();
Check(ImGui.Items.Count == 2, "ModMenu.Attributes alone must not suppress the fallback.");

AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("ModMenu"), AssemblyBuilderAccess.Run);
ImGui.Items.Clear();
game.DrawProgramMenusHook();
Check(ImGui.Items.Count == 0, "Loaded ModMenu suppresses both fallback items.");

ImGui.Clicked = "Oh My Stars Window";
OhMyStarsEntryPoint.DrawMenu();
Check(OhMyStarsWindow.IsOpen, "Existing ModMenu main entry still toggles open.");
OhMyStarsEntryPoint.DrawMenu();
Check(!OhMyStarsWindow.IsOpen, "Existing ModMenu main entry still toggles closed.");
ImGui.Clicked = "Star Editor";
OhMyStarsEntryPoint.DrawMenu();
Check(!StarsEditWindow.IsOpen, "Existing ModMenu editor entry still toggles closed.");
OhMyStarsEntryPoint.DrawMenu();
Check(StarsEditWindow.IsOpen, "Existing ModMenu editor entry still toggles open.");

new Harmony("nanonestor.ohmystars").UnpatchAll("nanonestor.ohmystars");
Console.WriteLine("Menu checks passed: hook registration, labels, open semantics, optional ModMenu and existing toggles.");
