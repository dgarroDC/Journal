using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Journal;

[HarmonyPatch]
public class Patches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ShipLogController), nameof(ShipLogController.LateInitialize))]
    private static void ShipLogController_LateInitialize()
    {
        Journal.Instance.Setup();
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ShipLogController), nameof(ShipLogController.Update))]
    private static void ShipLogController_Update(ShipLogController __instance)
    {
        // I guess this could be done in the mod Update instead of a patch, but the ShipLogController instance is handy...
        Journal.Instance.ShipLogControllerUpdate(__instance);
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CursorManager), nameof(CursorManager.RefreshCursorState))]
    private static void CursorManager_RefreshCursorState()
    {
        // We need a patch, if we use LateUpdate() hack sometimes the cursor is visible for some reason
        Journal.Instance.RefreshCursorState();
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InputField), "OnFocus")]
    private static bool InputField_OnFocus(InputField __instance)
    {
        if (Journal.Instance.ShouldMoveCaretToEndOnFocus(__instance))
        {
            __instance.MoveTextEnd(false);
            return false;
        }
        // Do the default select all text action
        return true;
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InputField), "KeyPressed")]
    private static bool InputField_KeyPressed(InputField __instance, Event evt)
    {
        if (Journal.Instance.UsingInput(__instance))
        {
            // Prevent "submit" or "cancel" actions
            KeyCode keyCode = evt.keyCode;
            bool wouldSubmit = __instance.lineType != InputField.LineType.MultiLineNewline && 
                               (keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter);
            bool wouldCancel = keyCode == KeyCode.Escape;
            if (wouldSubmit || wouldCancel)
            {
                // Prevent returning EditState.Finish (returns null?) and setting m_WasCanceled (if wouldCancel)
                // Thankfully returning null works because the result is only compared to Finish,
                // otherwise it could get hard because not accessible enum type...
                // Maybe this could be a postfix though, setting the result to null and m_WasCanceled to false...
                return false;
            }
        }
        return true;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(InputField), "SetDrawRangeToContainCaretPosition")]
    private static void InputField_SetDrawRangeToContainCaretPosition(InputField __instance,
        TextGenerator ___m_InputTextCache,
        ref int ___m_DrawStart,
        ref int ___m_DrawEnd)
    {
        // Keep the line count check just in case...
        // Also null check because we aren't using the property (although it should be initialized at this point)
        if (Journal.Instance.UsingInput(__instance) && !__instance.multiLine && 
            ___m_InputTextCache != null && ___m_InputTextCache.lineCount > 0)
        {
            // Prevent weird behaviour were only a portion of the text is visible when using the same char (TextGenerator issue?)
            // Just show the whole text, we know in all situations we want to show the complete entry name (unlike description)
            ___m_DrawStart = 0;
            ___m_DrawEnd = __instance.text.Length;
        }
    }
}
