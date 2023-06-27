using System.Reflection;
using HarmonyLib;
using Journal.External;
using OWML.ModHelper;
using UnityEngine;
using UnityEngine.UI;

namespace Journal;

public class Journal : ModBehaviour
{
    public static Journal Instance;

    private bool _setupDone;
    private JournalStore _store;
    private JournalMode _journalMode;

    private void Start()
    {
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        Instance = this;
        LoadManager.OnCompleteSceneLoad += (_, _) => _setupDone = false;
    }

    public void Setup()
    {
        // Just copying this from Epicas, waiting for save framework!
        string profileName = StandaloneProfileManager.SharedInstance?.currentProfile?.profileName ??
                             "XboxGamepassDefaultProfile";
        _store = new JournalStore(profileName);
        CreateMode();
    }

    private void CreateMode()
    {
        ICustomShipLogModesAPI customShipLogModesAPI =
            ModHelper.Interaction.TryGetModApi<ICustomShipLogModesAPI>("dgarro.CustomShipLogModes");

        customShipLogModesAPI.ItemListMake(true, true, itemList =>
        {
            _journalMode = itemList.gameObject.AddComponent<JournalMode>();
            _journalMode.ItemList = new ItemListWrapper(customShipLogModesAPI, itemList);
            _journalMode.Store = _store;
            _journalMode.gameObject.name = nameof(JournalMode);
            customShipLogModesAPI.AddMode(_journalMode, () => true, () => JournalMode.Name);

            _setupDone = true;
        });
    }

    public void ShipLogControllerUpdate(ShipLogController shipLogController)
    {
        if (!_setupDone) return; // idk if necessary but just in case...

        // Hack to workaround the early return because of input mode...
        // The first condition is probably redundant
        if (shipLogController._currentMode == _journalMode && _journalMode.UsingInput())
        {
            shipLogController._exitPrompt.SetVisibility(_journalMode.AllowCancelInput()); // This should be false
            _journalMode.UpdateMode();
        }
    }

    public void RefreshCursorState()
    {
        if (!_setupDone) return;

        if (_journalMode.UsingInput())
        {
            // We use KeyboardInput and want to use the cursor, but visible through a UI element
            Cursor.visible = false;
        }
    }

    public bool ShouldMoveCaretToEndOnFocus(InputField inputField)
    {
        return UsingInput(inputField)
               && !_journalMode.CreatingNewEntry(); // We want to select the whole default text when creating
    }

    public bool UsingInput(InputField inputField)
    {
        return _setupDone &&
               _journalMode.UsingInput() &&
               _journalMode.OwnsInputField(inputField);
    }
}
