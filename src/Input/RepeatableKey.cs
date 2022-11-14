namespace MyGame.Input;

public class RepeatableKey
{
    public float RepeatTimer;
    public bool WasRepeated;

    public void Update(bool isHeld, float deltaSeconds)
    {
        WasRepeated = false;
        RepeatTimer = isHeld ? RepeatTimer + deltaSeconds : 0;
        if (RepeatTimer >= InputHandler.INITIAL_REPEAT_DELAY + InputHandler.REPEAT_DELAY)
        {
            WasRepeated = true;
            RepeatTimer -= InputHandler.REPEAT_DELAY;
        }
    }
}
