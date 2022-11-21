namespace MyGame.Screens;

public class MenuHandler
{
    public MyGameMain Game { get; }

    public MainMenuScreen MainMenuScreen;
    public PauseMenu PauseScreen;
    public OptionsMenuScreen OptionsScreen;
    
    public MenuHandler(MyGameMain game)
    {
        Game = game;
        
        MainMenuScreen = new MainMenuScreen(game);
        PauseScreen = new PauseMenu(game);
        OptionsScreen = new OptionsMenuScreen(game);
    }
}
