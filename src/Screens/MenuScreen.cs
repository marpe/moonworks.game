﻿using MyGame.Graphics;

namespace MyGame.Screens;

public class MenuItem
{
    public string Text;
    public Action Callback;

    public MenuItem(string text, Action callback)
    {
        Text = text;
        Callback = callback;
    }
}

public class MenuScreen
{
    public bool IsHidden { get; private set; }

    private MyGameMain _game;

    private int _selectedIndex = -1;
    private List<MenuItem> _menuItems;

    public MenuScreen(MyGameMain game)
    {
        _game = game;

        _menuItems = new(new[]
        {
            new MenuItem("Play", OnPlay),
            new MenuItem("Options", () => {}),
            new MenuItem("Quit", OnQuit),
        });

        _selectedIndex = 0;
    }


    private void OnPlay()
    {
    }

    private void OnQuit()
    {
        _game.Quit();
    }

    public void Draw(Renderer renderer)
    {
        if (!IsHidden)
        {
            renderer.DrawRect(new Rectangle(0, 0, (int)renderer.SwapTexture.Width, (int)renderer.SwapTexture.Height), Color.Black * 0.5f);

            var center = new Vector2(renderer.SwapTexture.Width * 0.5f, renderer.SwapTexture.Height * 0.5f);
            var position = center;
            var lineHeight = 30;

            for (var i = 0; i < _menuItems.Count; i++)
            {
                var color = _selectedIndex == i ? Color.Red : Color.White;
                renderer.DrawText(_menuItems[i].Text, position, 0, color);
                position.Y += lineHeight;
            }
        }

        var viewProjection = SpriteBatch.GetViewProjection(0, 0, renderer.SwapTexture.Width, renderer.SwapTexture.Height);
        renderer.FlushBatches(renderer.SwapTexture, viewProjection);
    }

    public void Update(float deltaSeconds, bool allowKeyboardInput, bool allowMouseInput)
    {
        var input = _game.InputHandler;

        if (allowKeyboardInput)
        {
            if (input.IsKeyPressed(KeyCode.Escape))
            {
                IsHidden = !IsHidden;
            }

            if (input.IsKeyPressed(KeyCode.Down) || input.IsKeyPressed(KeyCode.S))
            {
                _selectedIndex = (_selectedIndex + 1) % _menuItems.Count;
            }
            else if (input.IsKeyPressed(KeyCode.Up) || input.IsKeyPressed(KeyCode.W))
            {
                _selectedIndex = (_menuItems.Count + _selectedIndex - 1) % _menuItems.Count;
            }

            if (input.IsKeyPressed(KeyCode.Return) || input.IsKeyPressed(KeyCode.Space))
            {
                if (_selectedIndex != -1)
                {
                    _menuItems[_selectedIndex].Callback.Invoke();
                }
            }
        }
    }
}