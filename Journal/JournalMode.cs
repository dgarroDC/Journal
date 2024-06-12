using System;
using System.Collections.Generic;
using Journal.External;
using OWML.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Journal;

public class JournalMode : ShipLogMode
{
    public const string Name = "Journal";

    public ItemListWrapper ItemList;
    public JournalStore Store;
    
    private OWAudioSource _oneShotSource;
    private readonly AudioType _openSound = AudioType.DialogueEnter;
    private readonly AudioType _positiveSound = AudioType.DialogueAdvance;
    private readonly AudioType _negativeSound = AudioType.DialogueExit;

    private State _currentState;
    private bool _creatingNewEntry;
    private bool _dontChoosePhotoOnNextRelease; // too hacky?
    private bool _pendingSave;
    private IEpicasAlbumAPI _epicasAlbumAPI;

    private Image _photo;
    private Text _questionMark;
    private string _questionMarkDefaultText;
    private List<InputField> _entryInputs;
    private InputField _descInput;
    private RectTransform _reticle;

    private readonly Color _selectionTextColor = new(0f, 0.2f, 0.3f);
    private readonly Color _editingTextColor = new(0.7f, 1f, 0.5f);
    private readonly Color _deletingTextColor = Color.red;
    private Color _prevTextColor;

    private ScreenPromptList _upperRightPromptList;
    private ScreenPromptList _mainPromptList;
    private CanvasGroupAnimator _mainPromptListAnimator;
    private readonly Vector3 _mainPromptListShownScale = Vector3.one * 1.5f; // Other prompts are with this scale (in root)...
    private readonly Vector3 _mainPromptListHiddenScale = new Vector3(0f, 1f, 1f) * 1.5f;
    private bool _usingGamepad;
    private ScreenPrompt _createEntryPromptKbm;
    private ScreenPrompt _createEntryPromptGamepad;
    private ScreenPrompt _renameEntryPromptKbm;
    private ScreenPrompt _renameEntryPromptGamepad;
    private ScreenPrompt _editDescriptionPromptKbm;
    private ScreenPrompt _editDescriptionPromptGamepad;
    private ScreenPrompt _setPhotoPrompt;
    private ScreenPrompt _removePhotoPrompt;
    private ScreenPrompt _toggleMoreToExplorePrompt;
    private ScreenPrompt _moveEntryPrompt;
    private ScreenPrompt _deleteEntryPrompt;
    private ScreenPrompt _togglePromptsPrompt;
    private ScreenPrompt _confirmInputPromptKbm;
    private ScreenPrompt _confirmInputPromptGamepad;
    private ScreenPrompt _discardInputPromptKbm;
    private ScreenPrompt _discardInputPromptGamepad;
    private ScreenPrompt _confirmDeletePrompt;
    private ScreenPrompt _cancelDeletePrompt;

    public enum State
    {
        Disabled,
        Main,
        Renaming,
        EditingDescription,
        Deleting,
        ChoosingPhoto
    }
    
    public override void Initialize(ScreenPromptList centerPromptList, ScreenPromptList upperRightPromptList, OWAudioSource oneShotSource)
    {
        _oneShotSource = oneShotSource;
        _upperRightPromptList = upperRightPromptList;

        ItemList.SetName(Name);
        _photo = ItemList.GetPhoto();
        _photo.preserveAspect = true; // Maybe this should be the default value...
        _questionMark = ItemList.GetQuestionMark();
        _questionMarkDefaultText = _questionMark.text;

        _epicasAlbumAPI = Journal.Instance.ModHelper.Interaction
            .TryGetModApi<IEpicasAlbumAPI>("dgarro.EpicasAlbum");
        
        SetupInputFields();
        SetupRaycastAndCursor();
        SetupPrompts();

        _currentState = State.Disabled;
    }

    private void SetupInputFields()
    {
        _entryInputs = new List<InputField>();
        foreach (ShipLogEntryListItem entryListItem in ItemList.GetItemsUI())
        {
            Text text = entryListItem._nameField;
            InputField input = AddInputFieldInput(text);
            input.characterLimit = 40; // Room for extra icons just in case...
            _entryInputs.Add(input);
        }

        ItemList.DescriptionFieldClear();
        GameObject itemTemplate = ItemList.DescriptionFieldGetNextItem().gameObject;
        // We want the input to occupy the whole visible field
        // TODO: Issues with Large UI? Not always matching the shared description field or something?
        GameObject descInputGO = Instantiate(itemTemplate.gameObject, itemTemplate.transform.parent.parent);
        descInputGO.name = "DescriptionInput";
        RectTransform descInputRT = descInputGO.GetComponent<RectTransform>();
        // We want space down to the bottom of the description field (8 lines),
        // although that line would cause the desc field to enable scroll if we used an item,
        // the characters are that are totally visible afaik, but not the bottom part of the caret...
        // TODO: Better scroll? A way to indicate the user that there is text above o below the visible?
        descInputRT.sizeDelta = new Vector2(descInputRT.sizeDelta.x, 196); // x is a bit below the top border
        Text firstDescText = descInputGO.GetComponent<Text>();
        _descInput = AddInputFieldInput(firstDescText);
        _descInput.lineType = InputField.LineType.MultiLineNewline;
        // Don't show this line, although it would be nice to generate them while editing...
        Destroy(descInputRT.Find("EntryBorderLine").gameObject);
        // Also we don't need this component
        descInputGO.DestroyAllComponents<ShipLogFactListItem>();
    }
    
    
    private void SetupRaycastAndCursor()
    {
        // GameObject.Find("Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/");
        GameObject canvas = GetComponentInParent<Canvas>().gameObject;
        canvas.AddComponent<GraphicRaycaster>();
        
        // Maybe I should should just find the ones bothering me?
        foreach (Image image in canvas.GetComponentsInChildren<Image>())
        {
            image.raycastTarget = false;
        }
        _questionMark.raycastTarget = false; // This is one is bothering in part of desc field...

        GameObject reticleGO = new GameObject("Reticle", typeof(Text));
        _reticle = reticleGO.transform as RectTransform;
        _reticle.parent = transform;
        _reticle.localPosition = Vector3.zero;
        _reticle.localEulerAngles = Vector3.zero;
        _reticle.localScale = Vector3.one;
        _reticle.anchorMin = Vector2.zero;
        _reticle.anchorMax = Vector2.one;
        _reticle.pivot = new Vector2(0.5f, 0.5f);
        _reticle.sizeDelta = Vector2.zero;
        Text reticleText = _reticle.GetComponent<Text>();
        reticleText.font = _questionMark.font;
        reticleText.text = "+";
        reticleText.alignment = TextAnchor.MiddleCenter;
        reticleText.fontSize = 45;
        reticleText.color = _questionMark.color;
        reticleText.raycastTarget = false;
    }

    private void SetupPrompts()
    {
        // TODO: Translate
        // Not using the ScreenPrompt.MultiCommandType.HOLD_ONE_AND_PRESS_2ND, too much space...
        // Some are created with empty string because their texts are set in UpdatePrompts()
        string holdPrompt = UITextLibrary.GetString(UITextType.HoldPrompt);
        string keyboardRequiredPrompt = "<color=red>(Keyboard required)</color>";
        _createEntryPromptKbm = new ScreenPrompt(InputLibrary.shiftL, InputLibrary.enter,
            $"Create Entry <CMD1>{holdPrompt} +<CMD2>", ScreenPrompt.MultiCommandType.CUSTOM_BOTH);
        _createEntryPromptGamepad = new ScreenPrompt($"Create Entry {keyboardRequiredPrompt}");
        _renameEntryPromptKbm = new ScreenPrompt(InputLibrary.enter, $"Rename Entry <CMD>{holdPrompt}");
        _renameEntryPromptGamepad = new ScreenPrompt($"Rename Entry {keyboardRequiredPrompt}");
        _editDescriptionPromptKbm = new ScreenPrompt(InputLibrary.enter, "Edit Description");
        _editDescriptionPromptGamepad = new ScreenPrompt($"Edit Description {keyboardRequiredPrompt}");
        _setPhotoPrompt = new ScreenPrompt(InputLibrary.toolActionPrimary, "");
        _removePhotoPrompt = new ScreenPrompt(InputLibrary.toolActionPrimary, $"Remove Photo <CMD>{holdPrompt}");
        _toggleMoreToExplorePrompt = new ScreenPrompt(InputLibrary.thrustDown, "");
        _moveEntryPrompt = new ScreenPrompt(InputLibrary.thrustUp, $"Move Entry <CMD>{holdPrompt}");
        _deleteEntryPrompt = new ScreenPrompt(InputLibrary.toolActionSecondary, "Delete Entry");
        
        _togglePromptsPrompt = new ScreenPrompt(InputLibrary.map, "");
        _confirmInputPromptKbm = new ScreenPrompt(InputLibrary.escape, "Confirm Text");
        _confirmInputPromptGamepad = new ScreenPrompt($"Confirm Text {keyboardRequiredPrompt}");
        _discardInputPromptKbm = new ScreenPrompt(InputLibrary.shiftL, InputLibrary.escape, 
            $"Discard Changes <CMD1>{holdPrompt} +<CMD2>", ScreenPrompt.MultiCommandType.CUSTOM_BOTH);
        _discardInputPromptGamepad = new ScreenPrompt($"Discard Text {keyboardRequiredPrompt}");
        _confirmDeletePrompt = new ScreenPrompt(InputLibrary.interact, $"Confirm Delete {holdPrompt}");
        _cancelDeletePrompt = new ScreenPrompt(InputLibrary.cancel, $"Cancel Delete");

        GameObject promptListGo = new GameObject("PromptList", 
            typeof(VerticalLayoutGroup), typeof(ScreenPromptList), typeof(CanvasGroupAnimator));
        RectTransform promptListRect = promptListGo.GetComponent<RectTransform>();
        promptListRect.parent = transform;
        promptListRect.localPosition = Vector3.zero;
        promptListRect.localEulerAngles = Vector3.zero;
        promptListRect.localScale = Vector3.one * 1.5f;
        promptListRect.anchoredPosition = new Vector2(140, 225); // Hardcoded floating thing...
        promptListRect.sizeDelta = new Vector2(150, 200);
        promptListRect.pivot = new Vector2(1f, 1f); // important x for the animation from left
        _mainPromptList = promptListGo.GetComponent<ScreenPromptList>();
        _mainPromptList.SetMinElementDimensionsAndFontSize(17, 17, 11);
        VerticalLayoutGroup promptListLayoutGroup = promptListGo.GetComponent<VerticalLayoutGroup>();
        promptListLayoutGroup.childAlignment = TextAnchor.UpperRight;
        promptListLayoutGroup.spacing = 9; // This is needed from Outer Wilds Patch 14 for some reason...
        promptListLayoutGroup.childForceExpandWidth = false;
        promptListLayoutGroup.childForceExpandHeight = false; // TODO: true except with 0 entries?
        _mainPromptListAnimator = promptListGo.GetComponent<CanvasGroupAnimator>();
        _mainPromptListAnimator.SetImmediate(1f, Store.Data.ShowPrompts ? _mainPromptListShownScale : _mainPromptListHiddenScale);
    }

    private InputField AddInputFieldInput(Text text)
    {
        InputField input = text.gameObject.AddComponent<InputField>();
        input.textComponent = text;
        input.caretWidth = 2; // Otherwise it's too thin
        input.selectionColor = _selectionTextColor; // Important alpha=1 for overlap in description field
        input.transition = Selectable.Transition.None;
        input.enabled = false;
        return input;
    }

    public bool UsingInput()
    {
        return _currentState is State.Renaming or State.EditingDescription;
    }
    
    public bool OwnsInputField(InputField inputField)
    {
        return _entryInputs.Contains(inputField) || _descInput == inputField;
    }

    public bool CreatingNewEntry()
    {
        return _creatingNewEntry;
    }

    public override void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)
    {
        OpenList();
        UpdateItems();
        UpdateDescriptionField();
        UpdatePhoto();

        if (_currentState != State.Disabled)
        {
            Journal.Instance.ModHelper.Console.WriteLine($"Unexpected state {_currentState} on enter!", MessageType.Error);
        }
        _currentState = State.Main;
    }

    private void OpenList()
    {
        _oneShotSource.PlayOneShot(_openSound, 3f); // Or TH_ProjectorActivate?
        ItemList.Open();
        AddPrompts();
    }

    private void AddPrompts()
    {
        PromptManager promptManager = Locator.GetPromptManager();
        _usingGamepad = OWInput.UsingGamepad();
        if (!_usingGamepad)
        {
            // Important because things could break or something...
            promptManager.AddScreenPrompt(_createEntryPromptKbm, _mainPromptList, TextAnchor.MiddleRight);
            promptManager.AddScreenPrompt(_renameEntryPromptKbm, _mainPromptList, TextAnchor.MiddleRight);
            promptManager.AddScreenPrompt(_editDescriptionPromptKbm, _mainPromptList, TextAnchor.MiddleRight);

            promptManager.AddScreenPrompt(_discardInputPromptKbm, _upperRightPromptList, TextAnchor.MiddleRight);
            promptManager.AddScreenPrompt(_confirmInputPromptKbm, _upperRightPromptList, TextAnchor.MiddleRight);
        }
        else
        {
            promptManager.AddScreenPrompt(_createEntryPromptGamepad, _mainPromptList, TextAnchor.MiddleRight);
            promptManager.AddScreenPrompt(_renameEntryPromptGamepad, _mainPromptList, TextAnchor.MiddleRight);
            promptManager.AddScreenPrompt(_editDescriptionPromptGamepad, _mainPromptList, TextAnchor.MiddleRight);

            promptManager.AddScreenPrompt(_discardInputPromptGamepad, _upperRightPromptList, TextAnchor.MiddleRight);
            promptManager.AddScreenPrompt(_confirmInputPromptGamepad, _upperRightPromptList, TextAnchor.MiddleRight);
        }

        promptManager.AddScreenPrompt(_setPhotoPrompt, _mainPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_removePhotoPrompt, _mainPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_toggleMoreToExplorePrompt, _mainPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_moveEntryPrompt, _mainPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_deleteEntryPrompt, _mainPromptList, TextAnchor.MiddleRight);

        promptManager.AddScreenPrompt(_togglePromptsPrompt, _upperRightPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_cancelDeletePrompt, _upperRightPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_confirmDeletePrompt, _upperRightPromptList, TextAnchor.MiddleRight); // We want this on left
    }

    private void RemovePrompts()
    {
        PromptManager promptManager = Locator.GetPromptManager();
        promptManager.RemoveScreenPrompt(_createEntryPromptKbm);
        promptManager.RemoveScreenPrompt(_createEntryPromptGamepad);
        promptManager.RemoveScreenPrompt(_renameEntryPromptKbm);
        promptManager.RemoveScreenPrompt(_renameEntryPromptGamepad);
        promptManager.RemoveScreenPrompt(_editDescriptionPromptKbm);
        promptManager.RemoveScreenPrompt(_editDescriptionPromptGamepad);
        promptManager.RemoveScreenPrompt(_setPhotoPrompt);
        promptManager.RemoveScreenPrompt(_removePhotoPrompt);
        promptManager.RemoveScreenPrompt(_toggleMoreToExplorePrompt);
        promptManager.RemoveScreenPrompt(_moveEntryPrompt);
        promptManager.RemoveScreenPrompt(_deleteEntryPrompt);

        promptManager.RemoveScreenPrompt(_togglePromptsPrompt);
        promptManager.RemoveScreenPrompt(_confirmInputPromptKbm);
        promptManager.RemoveScreenPrompt(_confirmInputPromptGamepad);
        promptManager.RemoveScreenPrompt(_discardInputPromptKbm);
        promptManager.RemoveScreenPrompt(_discardInputPromptGamepad);
        promptManager.RemoveScreenPrompt(_confirmDeletePrompt);
        promptManager.RemoveScreenPrompt(_cancelDeletePrompt);
    }

    private void UpdateItems()
    {
        List<Tuple<string,bool,bool,bool>> items = new();
        string rumorColor = ColorUtility.ToHtmlStringRGBA(Locator.GetUIStyleManager().GetShipLogRumorColor());
        string deletingColor = ColorUtility.ToHtmlStringRGBA(_deletingTextColor);
        for (var i = 0; i < Store.Data.Entries.Count; i++)
        {
            JournalStore.Entry entry = Store.Data.Entries[i];
            string name = entry.Name;
            string color = null;
            if (_currentState == State.Deleting && ItemList.GetSelectedIndex() == i)
            {
                color = deletingColor;
            }
            else if (entry.EpicasAlbumSnapshotName == null)
            {
                // ShipLogEntryListItem.UpdateNameField() does it with the font color, but I would have to refresh all items
                // each time the UI is updated, so rich text is way easier....
                color = rumorColor;
            }
            if (color != null)
            {
                name = $"<color=#{color}>{name}</color>";
            }
            items.Add(new Tuple<string, bool, bool, bool>(name, false, false, entry.HasMoreToExplore));
        }
        ItemList.SetItems(items);
    }
    
    private void UpdateDescriptionField()
    {
        // Important to clear before the if, for example if the only entry was deleted
        ItemList.DescriptionFieldClear();
        if (Store.Data.Entries.Count > 0)
        {
            int selectedIndex = ItemList.GetSelectedIndex();
            JournalStore.Entry selectedEntry = Store.Data.Entries[selectedIndex];
            
            // Maybe there should be a flag of missing photo already or something, but this just works...
            string snapshotName = selectedEntry.EpicasAlbumSnapshotName;
            if (snapshotName != null)
            {
                Sprite snapshotSprite = _epicasAlbumAPI.GetSnapshotSprite(snapshotName);
                if (snapshotSprite == null)
                {
                    ShipLogFactListItem missingImageItem = ItemList.DescriptionFieldGetNextItem();
                    missingImageItem.DisplayText($"<color=red>Photo \"{snapshotName}\" not found in Épicas Album!</color>");
                }
            }

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
        else
        {
            ShipLogFactListItem item = ItemList.DescriptionFieldGetNextItem();
            // TODO: Translate
            item.DisplayText("The Journal is empty, create your first entry now!");
        }
    }

    private void UpdatePhoto()
    {
        if (Store.Data.Entries.Count > 0)
        {
            int selectedIndex = ItemList.GetSelectedIndex();
            JournalStore.Entry selectedEntry = Store.Data.Entries[selectedIndex];
            if (selectedEntry.EpicasAlbumSnapshotName != null)
            {
                Sprite snapshotSprite = _epicasAlbumAPI.GetSnapshotSprite(selectedEntry.EpicasAlbumSnapshotName);
                if (snapshotSprite != null)
                {
                    _questionMark.gameObject.SetActive(false);
                    _photo.gameObject.SetActive(true);
                    _photo.sprite = snapshotSprite;
                }
                else
                {
                    _questionMark.gameObject.SetActive(true);
                    _photo.gameObject.SetActive(false);
                    _questionMark.text = "<color=red>X</color>";
                }
            }
            else
            {
                _questionMark.gameObject.SetActive(true);
                _photo.gameObject.SetActive(false);
                _questionMark.text = _questionMarkDefaultText;
            }
        }
        else
        {
            _questionMark.gameObject.SetActive(false);
            _photo.gameObject.SetActive(false);
        }
    }

    public override void ExitMode()
    {
        // Handle electrical failure, the input cases are really important to avoid softlock
        _creatingNewEntry = false; // Important to do it before rename entry end to avoid going to description
        switch (_currentState)
        {
            case State.Renaming:
                RenameEntryEnd(false);
                break;
            case State.EditingDescription:
                EditDescriptionEnd(false);
                break;
            case State.ChoosingPhoto:
                // Just change to main here to avoid the log error, the dialog with close with null and handled there
                _currentState = State.Main;
                break;
            case State.Deleting:
                UnmarkForDeletion(); // Well, only the state to Main for the log is important here really
                break;
        }
        CloseList();
        // TODO: Save more often?
        if (_pendingSave)
        {
            Store.SaveToDisk();
            _pendingSave = false;
        }

        if (_currentState != State.Main)
        {
            Journal.Instance.ModHelper.Console.WriteLine($"Unexpected state {_currentState} on exit!", MessageType.Error);
        }
        _currentState = State.Disabled;
    }

    private void CloseList()
    {
        ItemList.Close();
        RemovePrompts();
    }

    public override void UpdateMode()
    {
        UpdatePrompts();
        UpdateCursor();

        bool shiftPressed = OWInput.IsPressed(InputLibrary.shiftL) || OWInput.IsPressed(InputLibrary.shiftR);
        switch (_currentState)
        {
            case State.Main:
                int prevSelectedIndex = ItemList.GetSelectedIndex();
                int delta = ItemList.UpdateList();
                if (delta != 0)
                {
                    bool movingEntry = OWInput.IsPressed(InputLibrary.thrustUp); // This is by default also shift in keyboard
                    if (movingEntry)
                    {
                        int newSelectedIndex = ItemList.GetSelectedIndex();
                        JournalStore.Entry prevSelectedEntry = Store.Data.Entries[prevSelectedIndex];
                        // Don't swap the elements, that is weird when changing first<->last
                        Store.Data.Entries.RemoveAt(prevSelectedIndex);
                        Store.Data.Entries.Insert(newSelectedIndex, prevSelectedEntry);
                        UpdateItems();
                        ItemList.UpdateListUI(); // Avoid ugly frame, show the updated list now
                        _pendingSave = true;
                        _oneShotSource.PlayOneShot(delta > 0 ? _positiveSound : _negativeSound, 3f);
                        return; // Don't do any additional action when moving (also no need to change description or photo)
                    }

                    UpdateDescriptionField();
                    UpdatePhoto();
                }

                if (OWInput.IsNewlyPressed(InputLibrary.map))
                {
                    TogglePrompts();
                    return;
                }
                
                // Keyboard-required actions, all with enter
                if (shiftPressed && OWInput.IsNewlyPressed(InputLibrary.enter))
                {
                    CreateEntry();
                }
                if (Store.Data.Entries.Count == 0)
                {
                    return;
                }
                if (OWInput.IsPressed(InputLibrary.enter, 0.4f))
                {
                    RenameEntry();
                }
                else if (OWInput.IsNewlyReleased(InputLibrary.enter)) // Released because the user may want to hold it...
                {
                    EditDescription();
                }
                
                else if (OWInput.IsNewlyPressed(InputLibrary.thrustDown))
                {
                    ToggleMoreToExplore();
                }
                else if (OWInput.IsNewlyReleased(InputLibrary.toolActionPrimary))
                {
                    if (!_dontChoosePhotoOnNextRelease)
                    {
                        ChoosePhoto();
                    }
                    _dontChoosePhotoOnNextRelease = false;
                    // TODO: This prevent any input to be processed this frame! Should many inputs be allowed per frame?
                }
                else if (OWInput.IsPressed(InputLibrary.toolActionPrimary, 0.6f))
                {
                    RemovePhoto();
                }
                else if (OWInput.IsNewlyPressed(InputLibrary.toolActionSecondary)) // Like Épicas
                {
                    MarkForDeletion();
                }
                break;
            case State.Renaming:
                if (OWInput.IsNewlyPressed(InputLibrary.escape))
                {
                    RenameEntryEnd(shiftPressed && !_creatingNewEntry);
                }
                break;
            case State.EditingDescription:
                if (OWInput.IsNewlyPressed(InputLibrary.escape))
                {
                    EditDescriptionEnd(shiftPressed && !_creatingNewEntry);
                }
                break;
            case State.Deleting:
                if (OWInput.IsNewlyPressed(InputLibrary.cancel))
                {
                    UnmarkForDeletion();
                }
                else if (OWInput.IsPressed(InputLibrary.interact, 0.7f)) // enter would edit description next frame (because not Newly)
                {
                    DeleteEntry();
                }
                break;
            case State.ChoosingPhoto:
                // Do nothing, we aren't actually active right now (should we change mode?)
                break;
            default:
                Journal.Instance.ModHelper.Console.WriteLine($"Unexpected state {_currentState} on update!", MessageType.Error);
                break;
        }
    }

    private void UpdateCursor()
    {
        bool useCursor = UsingInput();
        if (useCursor)
        {
            if (!_reticle.gameObject.activeSelf)
            {
                // Workaround to update the position on cursor enabled, otherwise for some reason the position
                // reads as the old value before disabling even if the cursor is in the middle of the screen,
                // the position is only corrected after moving or clicking it seems...
                InputState.Change(Mouse.current.position, new  Vector2(Screen.width/2, Screen.height/2));
            }
            Vector2 mousePos = Mouse.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(transform as RectTransform, mousePos,
                Locator.GetActiveCamera().mainCamera, out Vector2 position);
            _reticle.localPosition = new Vector3(position.x, position.y, _reticle.localPosition.z);
            bool pressed = Mouse.current.leftButton.isPressed;
            _reticle.localScale = Vector3.one * (pressed ? 0.85f : 1f);
        }
        _reticle.gameObject.SetActive(useCursor);
    }

    private void UpdatePrompts()
    {
        bool emptyJournal = Store.Data.Entries.Count == 0;
        JournalStore.Entry selectedEntry = emptyJournal ? null : Store.Data.Entries[ItemList.GetSelectedIndex()];

        bool usingGamepad = OWInput.UsingGamepad();
        if (usingGamepad != _usingGamepad)
        {
            // VERY IMPORTANT
            // TODO: Bug when choosing photo with one and ending when other? Duplicated Prompts? 
            RemovePrompts();
            AddPrompts();
        }

        _createEntryPromptKbm.SetVisibility(_currentState == State.Main && !usingGamepad);
        _createEntryPromptKbm.SetDisplayState(emptyJournal
            ? ScreenPrompt.DisplayState.Attention
            : ScreenPrompt.DisplayState.Normal);
        _createEntryPromptGamepad.SetVisibility(_currentState == State.Main && usingGamepad);
        _createEntryPromptGamepad.SetDisplayState(emptyJournal
            ? ScreenPrompt.DisplayState.Attention
            : ScreenPrompt.DisplayState.GrayedOut);
        _renameEntryPromptKbm.SetVisibility(_currentState == State.Main && !usingGamepad && !emptyJournal);
        _renameEntryPromptGamepad.SetVisibility(_currentState == State.Main && usingGamepad && !emptyJournal);
        _renameEntryPromptGamepad.SetDisplayState(ScreenPrompt.DisplayState.GrayedOut);
        _editDescriptionPromptKbm.SetVisibility(_currentState == State.Main && !usingGamepad && !emptyJournal);
        _editDescriptionPromptGamepad.SetVisibility(_currentState == State.Main && usingGamepad && !emptyJournal);
        _editDescriptionPromptGamepad.SetDisplayState(ScreenPrompt.DisplayState.GrayedOut);
        _setPhotoPrompt.SetVisibility(_currentState == State.Main && !emptyJournal);
        bool hasPhoto = selectedEntry?.EpicasAlbumSnapshotName != null;
        // TODO: Translation
        _setPhotoPrompt.SetText(hasPhoto ? "Change Photo" : "Add Photo");
        _removePhotoPrompt.SetVisibility(_currentState == State.Main && !emptyJournal && hasPhoto);
        _toggleMoreToExplorePrompt.SetVisibility(_currentState == State.Main && !emptyJournal);
        _toggleMoreToExplorePrompt.SetText(!emptyJournal && selectedEntry.HasMoreToExplore ? "Unmark More to Explore" : "Mark More to Explore");
        _moveEntryPrompt.SetVisibility(_currentState == State.Main && Store.Data.Entries.Count > 1);
        _deleteEntryPrompt.SetVisibility(_currentState == State.Main && !emptyJournal);

        _togglePromptsPrompt.SetVisibility(_currentState == State.Main);
        _togglePromptsPrompt.SetDisplayState(emptyJournal && !Store.Data.ShowPrompts ? ScreenPrompt.DisplayState.Attention : ScreenPrompt.DisplayState.Normal);
        _togglePromptsPrompt.SetText(Store.Data.ShowPrompts ? "Hide Prompts" : "Show Prompts");
        _confirmInputPromptKbm.SetVisibility(UsingInput() && !usingGamepad);
        _confirmInputPromptGamepad.SetVisibility(UsingInput() && usingGamepad);
        _confirmInputPromptGamepad.SetDisplayState(ScreenPrompt.DisplayState.GrayedOut);
        _discardInputPromptKbm.SetVisibility(UsingInput() && !usingGamepad && !_creatingNewEntry);
        _discardInputPromptGamepad.SetVisibility(UsingInput() && usingGamepad && !_creatingNewEntry);
        _discardInputPromptGamepad.SetDisplayState(ScreenPrompt.DisplayState.GrayedOut);
        _confirmDeletePrompt.SetVisibility(_currentState == State.Deleting);
        _cancelDeletePrompt.SetVisibility(_currentState == State.Deleting);
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
        UpdatePhoto();
        ItemList.UpdateListUI(); // We want to update the UI but not move because of renaming
        _creatingNewEntry = true; // Only really necessary to remember to go to edit description next
        RenameEntry();
        // No need to save file, it would be saved on description end
    }
    
    private void RenameEntry()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        InputField inputField = _entryInputs[ItemList.GetIndexUI(selectedIndex)];
        inputField.text = Store.Data.Entries[selectedIndex].Name;
        EnableInputField(inputField);
        _currentState = State.Renaming;
        _oneShotSource.PlayOneShot(_positiveSound, 3f);
    }

    private void RenameEntryEnd(bool discardChanges)
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        InputField inputField = _entryInputs[ItemList.GetIndexUI(selectedIndex)];
        if (!discardChanges)
        {
            Store.Data.Entries[selectedIndex].Name = inputField.text;
        }
        DisableInputField(inputField);
        UpdateItems();
        // This is also for an alpha or something in UI index 4 (might be required to restore rumor color too?),
        // noticeable for a frame or while editing description in entry
        ItemList.UpdateListUI(); 
        if (_creatingNewEntry)
        {
            EditDescription();
            // No need to save file, it would be saved on description end
        }
        else
        {
            _currentState = State.Main;
            _pendingSave |= !discardChanges;
            _oneShotSource.PlayOneShot(discardChanges ? _negativeSound : _positiveSound, 3f);
        }
    }

    private void EditDescription()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        _descInput.text = Store.Data.Entries[selectedIndex].Description;
        ItemList.DescriptionFieldClear();
        _descInput.gameObject.SetActive(true);
        EnableInputField(_descInput);
        _currentState = State.EditingDescription;
        _oneShotSource.PlayOneShot(_positiveSound, 3f);
    }

    private void EditDescriptionEnd(bool discardChanges)
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        if (!discardChanges)
        {
            Store.Data.Entries[selectedIndex].Description = _descInput.text;
        }
        DisableInputField(_descInput);
        _descInput.gameObject.SetActive(false);
        UpdateDescriptionField();
        _currentState = State.Main;
        _creatingNewEntry = false;
        _pendingSave |= !discardChanges;
        _oneShotSource.PlayOneShot(discardChanges ? _negativeSound : _positiveSound, 3f);
    }

    private void EnableInputField(InputField inputField)
    {
        inputField.enabled = true;
        OWInput.ChangeInputMode(InputMode.KeyboardInput);
        Locator.GetPauseCommandListener().AddPauseCommandLock();
        inputField.ActivateInputField();
        _prevTextColor = inputField.textComponent.color;
        inputField.textComponent.color = _editingTextColor;
    }

    private void DisableInputField(InputField inputField)
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
        UpdateItems();
        UpdateDescriptionField(); // Remember that it has an item for more to explore
        ItemList.UpdateListUI(); // To match the icon with the description already changed in this frame
        _pendingSave = true;
        _oneShotSource.PlayOneShot(Store.Data.Entries[selectedIndex].HasMoreToExplore ? _positiveSound : _negativeSound, 3f);
    }

    private void ChoosePhoto()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        JournalStore.Entry selectedEntry = Store.Data.Entries[selectedIndex];
        _epicasAlbumAPI.OpenSnapshotChooserDialog(selectedEntry.EpicasAlbumSnapshotName, ChoosePhotoEnd);
        _currentState = State.ChoosingPhoto;
        CloseList();
    }

    private void ChoosePhotoEnd(string selectedSnapshotName)
    {
        if (selectedSnapshotName != null)
        {
            int selectedIndex = ItemList.GetSelectedIndex();
            Store.Data.Entries[selectedIndex].EpicasAlbumSnapshotName = selectedSnapshotName;
            UpdatePhoto();
            UpdateDescriptionField(); // Just to clear the image not found error
            UpdateItems();
            ItemList.UpdateListUI(); // To remove rumor color if it had it
            _pendingSave = true;
        }
        // Wait a frame to avoid closing the mode on UpdateMode() if run after this on same frame
        // TODO: Can this cause problems when exiting on failure?
        Journal.Instance.ModHelper.Events.Unity.FireOnNextUpdate(() =>
        {
            // At this point we could be in Disabled because of electrical failure
            if (_currentState == State.ChoosingPhoto)
            {
                _currentState = State.Main;
                OpenList();   
            }
            else if (_currentState != State.Disabled)
            {
                Journal.Instance.ModHelper.Console.WriteLine($"Unexpected state {_currentState} on ChoosePhotoEnd!", MessageType.Error);
            }
        });
    }

    private void RemovePhoto()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        JournalStore.Entry selectedEntry = Store.Data.Entries[selectedIndex];
        if (selectedEntry.EpicasAlbumSnapshotName != null)
        {
            selectedEntry.EpicasAlbumSnapshotName = null;
            UpdatePhoto();
            UpdateDescriptionField(); // Just to clear the image not found error, although here it could look silly to always reset the scroll...
            UpdateItems();
            ItemList.UpdateListUI(); // For the rumor color
            _pendingSave = true;
            _oneShotSource.PlayOneShot(_negativeSound, 3f);
            _dontChoosePhotoOnNextRelease = true;
        }
    }

    private void MarkForDeletion()
    {
        _currentState = State.Deleting;
        // To add the deleting color we need to update items and UI, need to do after changing state
        // (we can't change the color of the text component because the rumor orange in rich text would override it!)
        UpdateItems();
        ItemList.UpdateListUI();
        _oneShotSource.PlayOneShot(_positiveSound, 3f);
    }
    
    private void UnmarkForDeletion()
    {
        _currentState = State.Main;
        UpdateItems();
        ItemList.UpdateListUI();
        _oneShotSource.PlayOneShot(_negativeSound, 3f);
    }

    private void DeleteEntry()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        List<JournalStore.Entry> entries = Store.Data.Entries;
        entries.RemoveAt(selectedIndex);
        if (selectedIndex >= entries.Count && entries.Count > 0)
        {
            // Same check as Épicas, idk if the -1 is bad but just in case...
            ItemList.SetSelectedIndex(selectedIndex - 1);
        }
        _currentState = State.Main; // Again, important to do before updating items, otherwise another item would be marked!
        UpdateItems();
        UpdateDescriptionField();
        UpdatePhoto();
        ItemList.UpdateListUI(); // To match the selected entry with the description already changed in this frame
        _pendingSave = true;
        _oneShotSource.PlayOneShot(_negativeSound, 3f);
    }

    private void TogglePrompts()
    {
        Store.Data.ShowPrompts = !Store.Data.ShowPrompts;
        _mainPromptListAnimator.AnimateTo(1f, 
                Store.Data.ShowPrompts? _mainPromptListShownScale : _mainPromptListHiddenScale, 0.1f);
        _oneShotSource.PlayOneShot(Store.Data.ShowPrompts ? _positiveSound : _negativeSound);
        _pendingSave = true;
    }

    public override bool AllowModeSwap()
    {
        return _currentState == State.Main;
    }

    public override bool AllowCancelInput()
    {
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
