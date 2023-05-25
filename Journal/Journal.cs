using Journal.CustomShipLogModes;
using OWML.ModHelper;
using UnityEngine;

namespace Journal;

public class Journal : ModBehaviour
{
    public static Journal Instance;

    private bool _setupDone;
    private JournalStore _store;
    private JournalMode _journalMode;

    private void Start()
    {
        Instance = this;
        // I guess I'm always doing this...
        ModHelper.HarmonyHelper.AddPostfix<ShipLogController>("LateInitialize", typeof(Journal), nameof(SetupPatch));
        LoadManager.OnCompleteSceneLoad += (_, _) => _setupDone = false;
    }
    
    private static void SetupPatch() {
        Instance.Setup();
    }

    private void Setup()
    {
        // Just copying this from Epicas, waiting for save framework!
        string profileName = StandaloneProfileManager.SharedInstance?.currentProfile?.profileName ?? "XboxGamepassDefaultProfile";
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

    private void Update()
    {
        if (!_setupDone) return;

        // Hack to get updates on other input mode... 
        if (_journalMode.inputOn)
        {
            _journalMode.UpdateMode();
        }
    }

    private void LateUpdate()
    {
        if (!_setupDone) return;

        // Maybe it would be better to patch CursorManager...
        if (_journalMode.inputOn)
        {
            Cursor.visible = false;
        }
    }
}
