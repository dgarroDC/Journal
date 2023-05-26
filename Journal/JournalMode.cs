﻿using System;
using System.Collections.Generic;
using Journal.CustomShipLogModes;
using OWML.Common;
using UnityEngine;
using UnityEngine.UI;

namespace Journal;

public class JournalMode : ShipLogMode
{
    public const string Name = "Journal";

    public ItemListWrapper ItemList;
    public JournalStore Store;
 
    private State _currentState;
    private bool _creatingNewEntry;

    private Image _photo;
    private Text _questionMark;
    private List<CustomInputField> _entryInputs;
    private CustomInputField _firstDescInput;
    private Image _firstDescBorderLine;

    private readonly Color _selectionTextColor = new(0f, 0.2f, 0.3f);
    private readonly Color _editingTextColor = new(0.6f, 1f, 0.75f);
    private Color _prevTextColor;

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
            CustomInputField input = AddInputFieldInput(text);
            // TODO: max length
            _entryInputs.Add(input);
        }
        
        ItemList.DescriptionFieldClear();
        Text firstDescText = ItemList.DescriptionFieldGetNextItem()._text;
        _firstDescInput = AddInputFieldInput(firstDescText);
        _firstDescInput.lineType = CustomInputField.LineType.MultiLineNewline;
        _firstDescBorderLine = _firstDescInput.transform.Find("EntryBorderLine").GetComponent<Image>();
        // TODO: idea: force expand height + not infinite panel (add to the mask thing?), sizedelta.y = 1 (for the last row... although active scrolling!)

        _currentState = State.Disabled;
    }

    private CustomInputField AddInputFieldInput(Text text)
    {
        CustomInputField input = text.gameObject.AddComponent<CustomInputField>();
        input.textComponent = text;
        input.caretWidth = 2; // Otherwise it's too thin
        input.selectionColor = _selectionTextColor; // Important alpha=1 for overlap in description field
        input.enabled = false;
        return input;
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
            items.Add(new Tuple<string, bool, bool, bool>(entry.Name, false, false, entry.HasMoreToExplore));
        }
        ItemList.SetItems(items);
    }
    
    private void UpdateDescriptionField()
    {
        if (Store.Data.Entries.Count > 0)
        {
            ItemList.DescriptionFieldClear();
            int selectedIndex = ItemList.GetSelectedIndex();
            JournalStore.Entry selectedEntry = Store.Data.Entries[selectedIndex];
            string description = selectedEntry.Description;
            string[] facts = description.Split(new[] { "\n\n" },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string fact in facts)
            {
                ShipLogFactListItem item = ItemList.DescriptionFieldGetNextItem();
                item.DisplayText(fact.TrimStart('\n'));
            }

            if (selectedEntry.HasMoreToExplore)
            {
                ShipLogFactListItem moreToExploreItem = ItemList.DescriptionFieldGetNextItem();
                // Like ShipLogEntryDescriptionField.SetEntry does...
                moreToExploreItem.DisplayText(UITextLibrary.GetString(UITextType.ShipLogMoreThere));
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
                int prevSelectedIndex = ItemList.GetSelectedIndex();
                if (ItemList.UpdateList() != 0)
                {
                    bool movingEntry = OWInput.IsPressed(InputLibrary.thrustUp);
                    if (movingEntry)
                    {
                        int newSelectedIndex = ItemList.GetSelectedIndex();
                        (Store.Data.Entries[prevSelectedIndex], Store.Data.Entries[newSelectedIndex]) =
                            (Store.Data.Entries[newSelectedIndex], Store.Data.Entries[prevSelectedIndex]);
                        UpdateItems();
                        ItemList.UpdateListUI(); // Avoid ugly frame, show the updated list now
                        return; // Don't do any additional action when moving (also no need to change description)
                    }

                    UpdateDescriptionField();
                }

                // Keyboard-required actions, all with enter
                bool shiftPressed = OWInput.IsPressed(InputLibrary.shiftL) || OWInput.IsPressed(InputLibrary.shiftR);
                if (OWInput.IsPressed(InputLibrary.enter, 0.5f))
                {
                    RenameEntry();
                }
                else if (shiftPressed && OWInput.IsNewlyPressed(InputLibrary.enter))
                {
                    CreateEntry();
                }
                else if (OWInput.IsNewlyReleased(InputLibrary.enter)) // Released because the user may want to hold it...
                {
                    EditDescription();
                }
                
                else if (OWInput.IsNewlyPressed(InputLibrary.interact))
                {
                    ToggleMoreToExplore();
                }
                break;
            case State.Renaming:
                if (OWInput.IsNewlyPressed(InputLibrary.escape))
                {
                    RenameEntryEnd();
                }
                break;
            case State.EditingDescription:
                if (OWInput.IsNewlyPressed(InputLibrary.escape))
                {
                    EditDescriptionEnd();
                }
                break;
            default:
                Journal.Instance.ModHelper.Console.WriteLine($"Unexpected state {_currentState} on update!", MessageType.Error);
                break;
        }
    }

    private void CreateEntry()
    {
        List<JournalStore.Entry> entries = Store.Data.Entries;
        int newIndex = entries.Count == 0 ? 0 : ItemList.GetSelectedIndex() + 1;
        JournalStore.Entry newEntry = new JournalStore.Entry();
        entries.Insert(newIndex, newEntry);
        ItemList.SetSelectedIndex(newIndex);
        UpdateItems();
        UpdateDescriptionField();
        ItemList.UpdateListUI(); // We want to update the UI but not move because of renaming
        _creatingNewEntry = true; // Only really necessary to remember to go to edit description next
        RenameEntry();
    }
    
    private void RenameEntry()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        CustomInputField inputField = _entryInputs[ItemList.GetIndexUI(selectedIndex)];
        inputField.text = Store.Data.Entries[selectedIndex].Name;
        EnableInputField(inputField);
        _currentState = State.Renaming;
    }

    private void RenameEntryEnd()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        CustomInputField inputField = _entryInputs[ItemList.GetIndexUI(selectedIndex)];
        Store.Data.Entries[selectedIndex].Name = inputField.text;
        DisableInputField(inputField);
        UpdateItems();
        if (_creatingNewEntry)
        {
            // TODO: Consider this in the "confirm" prompt?
            EditDescription();
        }
        else
        {
            _currentState = State.Main;
        }
    }

    private void EditDescription()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        _firstDescInput.text = Store.Data.Entries[selectedIndex].Description;
        ItemList.DescriptionFieldClear();
        ItemList.DescriptionFieldGetNextItem();
        EnableInputField(_firstDescInput);
        _firstDescBorderLine.enabled = false;
        _currentState = State.EditingDescription;
    }

    private void EditDescriptionEnd()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        Store.Data.Entries[selectedIndex].Description = _firstDescInput.text;
        DisableInputField(_firstDescInput);
        _firstDescBorderLine.enabled = true;
        UpdateDescriptionField();
        _currentState = State.Main;
        _creatingNewEntry = false;
    }

    private void EnableInputField(CustomInputField inputField)
    {
        inputField.onFocusSelectAll = _creatingNewEntry; // To quickly replace placeholder default text
        inputField.enabled = true;
        OWInput.ChangeInputMode(InputMode.KeyboardInput);
        Locator.GetPauseCommandListener().AddPauseCommandLock();
        inputField.ActivateInputField();
        _prevTextColor = inputField.textComponent.color;
        inputField.textComponent.color = _editingTextColor;
    }

    private void DisableInputField(CustomInputField inputField)
    {
        inputField.textComponent.color = _prevTextColor;
        inputField.DeactivateInputField();
        Locator.GetPauseCommandListener().RemovePauseCommandLock();
        OWInput.RestorePreviousInputs();
        inputField.enabled = false;
    }
    
    private void ToggleMoreToExplore()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        Store.Data.Entries[selectedIndex].HasMoreToExplore = !Store.Data.Entries[selectedIndex].HasMoreToExplore;
        UpdateItems(); // No need to update the UI this frame
        UpdateDescriptionField(); // Remember that it has an item for more to explore
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
