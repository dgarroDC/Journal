using System.IO;
using Journal.CustomShipLogModes;
using OWML.ModHelper;
using UnityEngine;

namespace Journal;

public class Journal : ModBehaviour
{
    public static Journal Instance;

    private JournalStore _store;

    private void Start()
    {
        Instance = this;
        // I guess I'm always doing this...
        ModHelper.HarmonyHelper.AddPostfix<ShipLogController>("LateInitialize", typeof(Journal), nameof(SetupPatch));
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
            JournalMode journalMode = itemList.gameObject.AddComponent<JournalMode>();
            journalMode.ItemList = new ItemListWrapper(customShipLogModesAPI, itemList);
            journalMode.Store = _store;
            journalMode.gameObject.name = nameof(JournalMode);
            customShipLogModesAPI.AddMode(journalMode, () => true, () => JournalMode.Name);
        });
    }
}
