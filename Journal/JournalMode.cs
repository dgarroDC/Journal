using System;
using System.Collections.Generic;
using Journal.CustomShipLogModes;
using OWML.Common;
using UnityEngine.UI;

namespace Journal;

public class JournalMode : ShipLogMode
{
    public const string Name = "Journal";

    public ItemListWrapper ItemList;
    public JournalStore Store;
 
    private State _currentState;
    private bool _shouldRenameNextUpdate;

    private Image _photo;
    private Text _questionMark;
    private List<CustomInputField> _entryInputs;
    private CustomInputField _firstDescInput;
    private Image _firstDescBorderLine;

    public enum State
    {
        Disabled,
        Main,
        Renaming,
        EditingDescription
    }
    
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
        _firstDescBorderLine = _firstDescInput.transform.Find("EntryBorderLine").GetComponent<Image>();
        // TODO: Selection color alpha=1
        // TODO: idea: force expand height + not infinite panel (add to the mask thing?), sizedelta.y = 1 (for the last row... although active scrolling!)
        // TODO: Clear + GetNextItem on edit desc

        _currentState = State.Disabled;
    }

    public bool UsingInput()
    {
        return _currentState is State.Renaming or State.EditingDescription;
    }

    public override void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)
    {
        ItemList.Open();
        UpdateItems();
        UpdateDescriptionField();

        if (_currentState != State.Disabled)
        {
            Journal.Instance.ModHelper.Console.WriteLine($"Unexpected state {_currentState} on enter!", MessageType.Error);
        }
        _currentState = State.Main;
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
    
    private void UpdateDescriptionField()
    {
        if (Store.Data.Entries.Count > 0)
        {
            ItemList.DescriptionFieldClear();
            int selectedIndex = ItemList.GetSelectedIndex();
            string description = Store.Data.Entries[selectedIndex].Description;
            string[] facts = description.Split(new[] { "\n\n" },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string fact in facts)
            {
                ShipLogFactListItem item = ItemList.DescriptionFieldGetNextItem();
                item.DisplayText(fact.TrimStart('\n'));
            }
        }
    }

    public override void ExitMode()
    {
        ItemList.Close();
        // TODO: Save more often?
        Store.SaveToDisk();

        if (_currentState != State.Main)
        {
            Journal.Instance.ModHelper.Console.WriteLine($"Unexpected state {_currentState} on exit!", MessageType.Error);
        }
        _currentState = State.Disabled;
    }

    public override void UpdateMode()
    {
        // TODO: Add Store.Data.Entries.Count > 0 check on most actions..
        // TODO: What happens if desc field is scrolled while editing???

        switch (_currentState)
        {
            case State.Main:
                if (ItemList.UpdateList() != 0)
                {
                    UpdateDescriptionField();
                    _shouldRenameNextUpdate = false; 
                    // Just in case it's moved just next frame after creation,
                    // we need to update the list after the creation
                    // (maybe CSLM should add UpdateListUI() without navigation?)
                }
            
                if (!_shouldRenameNextUpdate && OWInput.IsNewlyPressed(InputLibrary.interact)) // TODO: Hold enter?
                {
                    List<JournalStore.Entry> entries = Store.Data.Entries;
                    int newIndex = entries.Count == 0? 0 : ItemList.GetSelectedIndex() + 1;
                    JournalStore.Entry newEntry = new JournalStore.Entry();
                    entries.Insert(newIndex, newEntry);
                    UpdateItems();
                    UpdateDescriptionField();
                    ItemList.SetSelectedIndex(newIndex);
                    _shouldRenameNextUpdate = true;
                }
                else if (_shouldRenameNextUpdate || OWInput.IsNewlyPressed(InputLibrary.enter))
                {
                    if (_shouldRenameNextUpdate || OWInput.IsPressed(InputLibrary.shiftL) || OWInput.IsPressed(InputLibrary.shiftR))
                    {
                        int selectedIndex = ItemList.GetSelectedIndex();
                        CustomInputField inputField = _entryInputs[ItemList.GetIndexUI(selectedIndex)];
                        inputField.text = Store.Data.Entries[selectedIndex].Name;
                        EnableInputField(inputField);
                        _currentState = State.Renaming;
                        _shouldRenameNextUpdate = false;
                    }
                    else
                    {
                        int selectedIndex = ItemList.GetSelectedIndex();
                        _firstDescInput.text = Store.Data.Entries[selectedIndex].Description;
                        ItemList.DescriptionFieldClear();
                        ItemList.DescriptionFieldGetNextItem();
                        EnableInputField(_firstDescInput);
                        _firstDescBorderLine.enabled = false;
                        _currentState = State.EditingDescription; 
                    }
                }
                break;
            case State.Renaming:
                if (OWInput.IsNewlyPressed(InputLibrary.escape))
                {
                    int selectedIndex = ItemList.GetSelectedIndex();
                    CustomInputField inputField = _entryInputs[ItemList.GetIndexUI(selectedIndex)];
                    Store.Data.Entries[selectedIndex].Name = inputField.text;
                    DisableInputField(inputField);
                    UpdateItems();
                    _currentState = State.Main;
                }
                break;
            case State.EditingDescription:
                if (OWInput.IsNewlyPressed(InputLibrary.escape))
                {
                    int selectedIndex = ItemList.GetSelectedIndex();
                    Store.Data.Entries[selectedIndex].Description = _firstDescInput.text;
                    DisableInputField(_firstDescInput);
                    _firstDescBorderLine.enabled = true;
                    UpdateDescriptionField();
                    _currentState = State.Main;
                }
                break;
            default:
                Journal.Instance.ModHelper.Console.WriteLine($"Unexpected state {_currentState} on update!", MessageType.Error);
                break;
        }
    }

    private static void DisableInputField(CustomInputField inputField)
    {
        inputField.DeactivateInputField();
        Locator.GetPauseCommandListener().RemovePauseCommandLock();
        OWInput.RestorePreviousInputs();
        inputField.enabled = false;
    }

    private static void EnableInputField(CustomInputField inputField)
    {
        inputField.enabled = true;
        OWInput.ChangeInputMode(InputMode.KeyboardInput);
        Locator.GetPauseCommandListener().AddPauseCommandLock();
        inputField.ActivateInputField();
    }

    public override bool AllowModeSwap()
    {
        return _currentState == State.Main;
    }

    public override bool AllowCancelInput()
    {
        // TODO: fix _exitPrompt not changing!
        return _currentState == State.Main;
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
