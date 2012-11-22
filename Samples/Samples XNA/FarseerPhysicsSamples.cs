#region Using System
using System;
using System.Reflection;
using System.Collections.Generic;
#endregion
#region Using XNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
#endregion
#region Using Farseer
using FarseerPhysics.Samples.Demos;
using FarseerPhysics.Samples.ScreenSystem;
using FarseerPhysics.Samples.MediaSystem;
#endregion

namespace FarseerPhysics.Samples
{
  internal static class Program
  {
    /// <summary>
    /// The main entry point for the samples
    /// </summary>
    private static void Main(string[] args)
    {
      using (FarseerPhysicsSamples physicsSamples = new FarseerPhysicsSamples())
      {
        physicsSamples.Run();
      }
    }
  }

  /// <summary>
  /// This is the main type for the samples
  /// </summary>
  public class FarseerPhysicsSamples : Game
  {
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private LineBatch _lineBatch;
    private QuadRenderer _quadRenderer;

    private InputHelper _input;
    private FrameRateCounter _counter;

    private List<GameScreen> _screens = new List<GameScreen>();
    private List<GameScreen> _screensToUpdate = new List<GameScreen>();

    private List<RenderTarget2D> _transitions = new List<RenderTarget2D>();

    private bool _isExiting;
    private bool _showFPS;

    public FarseerPhysicsSamples()
    {
      Window.Title = "Farseer Physics Samples";
      _graphics = new GraphicsDeviceManager(this);
      _graphics.PreferMultiSampling = true;
      _graphics.PreferredBackBufferWidth = 1280;
      _graphics.PreferredBackBufferHeight = 720;
      ConvertUnits.SetDisplayUnitToSimUnitRatio(24f);
      IsFixedTimeStep = true;
#if WINDOWS
      _graphics.IsFullScreen = false;
#elif XBOX
      _graphics.IsFullScreen = true;
#endif

      Content.RootDirectory = "Content";
    }

    protected override void Initialize()
    {
      AssetCreator.Initialize(this);
      MediaManager.Initialize(this);

      _input = new InputHelper();
      _counter = new FrameRateCounter();

      _isExiting = false;
      _showFPS = false;

      base.Initialize();
    }

    protected override void LoadContent()
    {
      base.LoadContent();

      _spriteBatch = new SpriteBatch(GraphicsDevice);
      _lineBatch = new LineBatch(GraphicsDevice);

      _input.LoadContent(GraphicsDevice.Viewport);
      _counter.LoadContent();

      MenuScreen menuScreen = new MenuScreen("Farseer Physics Samples");
      menuScreen.AddMenuItem("Demos", EntryType.Separator, null);

      Assembly SamplesFramework = Assembly.GetExecutingAssembly();

      foreach (Type SampleType in SamplesFramework.GetTypes())
      {
        if (SampleType.IsSubclassOf(typeof(PhysicsGameScreen)))
        {
          PhysicsGameScreen DemoScreen = SamplesFramework.CreateInstance(SampleType.ToString()) as PhysicsGameScreen;
          menuScreen.AddMenuItem(DemoScreen.GetTitle(), EntryType.Screen, DemoScreen);
        }
      }

      menuScreen.AddMenuItem("", EntryType.Separator, null);
      menuScreen.AddMenuItem("Exit", EntryType.ExitItem, null);

      AddScreen(new BackgroundScreen());
      AddScreen(menuScreen);
      AddScreen(new LogoScreen(TimeSpan.FromSeconds(3.0)));

      ResetElapsedTime();
    }

    protected override void UnloadContent()
    {
      foreach (GameScreen screen in _screens)
      {
        screen.UnloadContent();
      }
      base.UnloadContent();
    }

    /// <summary>
    /// Allows each screen to run logic.
    /// </summary>
    protected override void Update(GameTime gameTime)
    {
      // Read the keyboard and gamepad.
      _input.Update(gameTime);
      // Update the framerate counter
      _counter.Update(gameTime);

      if (_input.IsNewKeyPress(Keys.F12))
      {
        _graphics.ToggleFullScreen();
      }

      if (_input.IsNewKeyPress(Keys.F11))
      {
        _showFPS = !_showFPS;
      }

      // Make a copy of the master screen list, to avoid confusion if
      // the process of updating one screen adds or removes others.
      _screensToUpdate.Clear();
      _screensToUpdate.AddRange(_screens);

      bool otherScreenHasFocus = !IsActive;
      bool coveredByOtherScreen = false;

      // Loop as long as there are screens waiting to be updated.
      while (_screensToUpdate.Count > 0)
      {
        // Pop the topmost screen off the waiting list.
        GameScreen screen = _screensToUpdate[_screensToUpdate.Count - 1];

        _screensToUpdate.RemoveAt(_screensToUpdate.Count - 1);

        // Update the screen.
        screen.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

        if (screen.ScreenState == ScreenState.TransitionOn || screen.ScreenState == ScreenState.Active)
        {
          // If this is the first active screen we came across,
          // give it a chance to handle input.
          if (!otherScreenHasFocus && !_isExiting)
          {
            _input.ShowCursor = screen.HasCursor;
            screen.HandleInput(_input, gameTime);
            otherScreenHasFocus = true;
          }

          // If this is an active non-popup, inform any subsequent
          // screens that they are covered by it.
          if (!screen.IsPopup)
          {
            coveredByOtherScreen = true;
          }
        }
      }

      if (_isExiting && _screens.Count == 0)
      {
        Exit();
      }

      base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
      int transitionCount = 0;
      foreach (GameScreen screen in _screens)
      {
        if (screen.ScreenState == ScreenState.TransitionOn || screen.ScreenState == ScreenState.TransitionOff)
        {
          transitionCount++;
          if (_transitions.Count < transitionCount)
          {
            PresentationParameters _pp = GraphicsDevice.PresentationParameters;
            _transitions.Add(new RenderTarget2D(GraphicsDevice, _pp.BackBufferWidth, _pp.BackBufferHeight, false,
                                                SurfaceFormat.Color, _pp.DepthStencilFormat, _pp.MultiSampleCount,
                                                RenderTargetUsage.DiscardContents));
          }
          GraphicsDevice.SetRenderTarget(_transitions[transitionCount - 1]);
          GraphicsDevice.Clear(Color.Transparent);
          screen.Draw(gameTime);
          GraphicsDevice.SetRenderTarget(null);
        }
      }

      GraphicsDevice.Clear(Color.Black);

      transitionCount = 0;
      foreach (GameScreen screen in _screens)
      {
        if (screen.ScreenState == ScreenState.Hidden)
        {
          continue;
        }

        if (screen.ScreenState == ScreenState.TransitionOn || screen.ScreenState == ScreenState.TransitionOff)
        {
          _spriteBatch.Begin(0, BlendState.AlphaBlend);
          _spriteBatch.Draw(_transitions[transitionCount], Vector2.Zero, Color.White * screen.TransitionAlpha);
          _spriteBatch.End();

          transitionCount++;
        }
        else
        {
          screen.Draw(gameTime);
        }
      }

      _input.Draw(_spriteBatch);
      if (_showFPS)
      {
        _counter.Draw(_spriteBatch);
      }

      base.Draw(gameTime);
    }

    public void ExitGame()
    {
      foreach (GameScreen screen in _screens)
      {
        screen.ExitScreen();
      }
      _isExiting = true;
    }

    /// <summary>
    /// Adds a new screen to the screen manager.
    /// </summary>
    public void AddScreen(GameScreen screen)
    {
      screen.Framework = this;
      screen.IsExiting = false;

      screen.Sprites = _spriteBatch;
      screen.Lines = _lineBatch;
      screen.Quads = _quadRenderer;

      // Tell the screen to load content.
      screen.LoadContent();
      _screens.Add(screen);
    }

    /// <summary>
    /// Removes a screen from the screen manager. You should normally
    /// use GameScreen.ExitScreen instead of calling this directly, so
    /// the screen can gradually transition off rather than just being
    /// instantly removed.
    /// </summary>
    public void RemoveScreen(GameScreen screen)
    {
      // Tell the screen to unload content.
      screen.UnloadContent();
      _screens.Remove(screen);
      _screensToUpdate.Remove(screen);
    }
  }
}