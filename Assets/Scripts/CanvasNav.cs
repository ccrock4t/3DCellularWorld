using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static WorldAutomaton;
using static WorldAutomaton.Elemental;

public class CanvasNav : MonoBehaviour
{
    public List<Button> buttons;
    static int buttonIndex = 0;

    // Start is called before the first frame update
    void Start()
    {
        if (buttons != null)
        {
            //If there are, select the first one
            buttons[0].Select();
            buttonIndex = 0;
        }
    }

    // Update is called once per frame
    void Update()
    {
		if (Input.GetKeyDown(KeyCode.Tab) && buttons.Count > 1)
		{

			buttons[buttonIndex].interactable = true;
			//if shift is not pressed, move down on the list - or, if at the bottom, move to the top
			if (buttons.Count <= buttonIndex + 1)

			{
				buttonIndex = -1;
			}
			CanvasNav.buttonIndex++;
			buttons[buttonIndex].interactable = false;
			
		}
	}

	public static Element GetButtonCellState()
    {
		return (Element)CanvasNav.buttonIndex;
    }
}
