using System;
using System.Collections.Generic;
using Journal.CustomShipLogModes;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace Journal;

public class JournalMode : ShipLogMode
{
    public const string Name = "Journal";

    public ItemListWrapper ItemList;
    public JournalStore Store;

    private Image _photo;
    private Text _questionMark;

    public override void Initialize(ScreenPromptList centerPromptList, ScreenPromptList upperRightPromptList, OWAudioSource oneShotSource)
    {
        ItemList.SetName(Name);
        _photo = ItemList.GetPhoto();
        _questionMark = ItemList.GetQuestionMark();
    }

    public override void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)
    {
        ItemList.Open();
        UpdeteItems();
    }

    private void UpdeteItems()
    {
        List<Tuple<string,bool,bool,bool>> items = new();
        foreach (JournalStore.Entry entry in Store.Data.Entries)
        {
            items.Add(new Tuple<string, bool, bool, bool>(entry.Name, false, false, false));
        }
        ItemList.SetItems(items);
    }

    public override void ExitMode()
    {
        ItemList.Close();
    }

    public override void UpdateMode()
    {
        ItemList.UpdateList();

        if (OWInput.IsNewlyPressed(InputLibrary.interact)) // TODO: Hold enter?
        {
            List<JournalStore.Entry> entries = Store.Data.Entries;
            int newIndex = entries.Count == 0? 0 : ItemList.GetSelectedIndex() + 1;
            JournalStore.Entry newEntry = new JournalStore.Entry("New Entry " + newIndex);
            entries.Insert(newIndex, newEntry);
            Store.SaveEntries();
            UpdeteItems();
            ItemList.SetSelectedIndex(newIndex);
            // Update List UI without changing pos?
        }
        else if (OWInput.IsNewlyPressed(InputLibrary.enter))
        {
            // TODO: Rename entry
        }
    }

    public override bool AllowModeSwap()
    {
        return true; // TODO
    }

    public override bool AllowCancelInput()
    {
        return true; // TODO
    }

    public override void OnEnterComputer()
    {
        // No-op
    }

    public override void OnExitComputer()
    {
        // No-op
    }    

    public override string GetFocusedEntryID()
    {
        return "";
    }
}
