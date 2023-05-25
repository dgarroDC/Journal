using System;
using System.Collections.Generic;
using Journal.CustomShipLogModes;
using UnityEngine.UI;

namespace Journal;

public class JournalMode : ShipLogMode
{
    public const string Name = "Journal";

    public ItemListWrapper ItemList;
    public JournalStore Store;

    public bool inputOn;

    private Image _photo;
    private Text _questionMark;
    private List<CustomInputField> _entryInputs;
    private CustomInputField _firstDescInput;

    public override void Initialize(ScreenPromptList centerPromptList, ScreenPromptList upperRightPromptList, OWAudioSource oneShotSource)
    {
        ItemList.SetName(Name);
        _photo = ItemList.GetPhoto();
        _questionMark = ItemList.GetQuestionMark();

        _entryInputs = new List<CustomInputField>();
        foreach (ShipLogEntryListItem entryListItem in ItemList.GetItemsUI())
        {
            Text text = entryListItem._nameField;
            CustomInputField input = text.gameObject.AddComponent<CustomInputField>();
            input.textComponent = text;
            input.enabled = false;
            // TODO: max length
            _entryInputs.Add(input);
        }
        
        ItemList.DescriptionFieldClear();
        Text firstDescText = ItemList.DescriptionFieldGetNextItem()._text;
        _firstDescInput = firstDescText.gameObject.AddComponent<CustomInputField>();
        _firstDescInput.textComponent = firstDescText;
        _firstDescInput.enabled = false;
        _firstDescInput.lineType = CustomInputField.LineType.MultiLineNewline;
        // TODO: Selection color alpha=1
        // TODO: Disable EntryBorderLine
        // TODO: idea: force expand height + not infinite panel (add to the mask thing?), sizedelta.y = 1 (for the last row... although active scrolling!)
        // TODO: Clear + GetNextItem on edit desc
    }

    public override void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)
    {
        ItemList.Open();
        UpdateItems();
    }

    private void UpdateItems()
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
        // TODO: Save more often?
        Store.SaveToDisk();
    }

    public override void UpdateMode()
    {
        // TODO: What happens if desc field is scrolled while editing???

        if (!inputOn)
        {
            ItemList.UpdateList();
            
            if (OWInput.IsNewlyPressed(InputLibrary.interact)) // TODO: Hold enter?
            {
                List<JournalStore.Entry> entries = Store.Data.Entries;
                int newIndex = entries.Count == 0? 0 : ItemList.GetSelectedIndex() + 1;
                JournalStore.Entry newEntry = new JournalStore.Entry("New Entry " + newIndex);
                entries.Insert(newIndex, newEntry);
                UpdateItems();
                ItemList.SetSelectedIndex(newIndex);
                // TODO: Update List UI without changing pos?
            }
            else if (OWInput.IsNewlyPressed(InputLibrary.enter))
            {
                int selectedIndex = ItemList.GetSelectedIndex();
                CustomInputField inputField = _firstDescInput; //_entryInputs[ItemList.GetIndexUI(selectedIndex)];
                inputField.text = Store.Data.Entries[selectedIndex].Name;
                inputField.enabled = true;
                OWInput.ChangeInputMode(InputMode.KeyboardInput);
                Locator.GetPauseCommandListener().AddPauseCommandLock();
                inputField.ActivateInputField();
                inputOn = true;
                // TODO: Fix not showing full text if all chars the same???
            }
        }
        else
        {
            if (OWInput.IsNewlyPressed(InputLibrary.escape))
            {
                int selectedIndex = ItemList.GetSelectedIndex();
                CustomInputField inputField = _firstDescInput; //_entryInputs[ItemList.GetIndexUI(selectedIndex)];
                Store.Data.Entries[selectedIndex].Name = inputField.text;
                UpdateItems();
                inputOn = false;
                inputField.DeactivateInputField();
                Locator.GetPauseCommandListener().RemovePauseCommandLock();
                OWInput.RestorePreviousInputs();
                inputField.enabled = false;
            }
        }
    }

    public override bool AllowModeSwap()
    {
        return !inputOn;
    }

    public override bool AllowCancelInput()
    {
        // TODO: fix _exitPrompt not changing!
        return !inputOn;
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
