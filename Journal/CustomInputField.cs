namespace Journal;

using UnityEngine.UI;

// https://answers.unity.com/questions/1294584/how-to-disable-selectall-text-of-inputfield-onfocu.html
public class CustomInputField : InputField
{
    public bool justFocused;
 
    new public void ActivateInputField()
    {
        justFocused = true;
        base.ActivateInputField();
    }
 
    protected override void LateUpdate()
    {
        base.LateUpdate();
        if(justFocused)
        {
            MoveTextEnd(true);
            justFocused = false;
        }
    }
}
