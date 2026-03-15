using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Checks for button input on an input action
/// </summary>
public class OnButtonPress : MonoBehaviour
{
    [Tooltip("Actions to check")]
    public InputAction action = null;

    // When the button is pressed
    public UnityEvent OnPress = new UnityEvent();

    // When the button is released
    public UnityEvent OnRelease = new UnityEvent();

    private void Awake()
    {
        if (action != null)
        {
            action.started += Pressed;
            action.canceled += Released;
        }
        else
        {
            Debug.LogError("InputAction not set on " + gameObject.name);
        }
    }

    private void OnDestroy()
    {
        if (action != null)
        {
            action.started -= Pressed;
            action.canceled -= Released;
        }
    }

    private void OnEnable()
    {
        if (action != null)
        {
            action.Enable();
        }
    }

    private void OnDisable()
    {
        if (action != null)
        {
            action.Disable();
        }
    }

    private void Pressed(InputAction.CallbackContext context)
    {
        if (OnPress != null)
        {
            OnPress.Invoke();
        }
        else
        {
            Debug.LogWarning("OnPress event is not assigned in " + gameObject.name);
        }
    }

    private void Released(InputAction.CallbackContext context)
    {
        if (OnRelease != null)
        {
            OnRelease.Invoke();
        }
        else
        {
            Debug.LogWarning("OnRelease event is not assigned in " + gameObject.name);
        }
    }
}
