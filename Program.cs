using McMaster.Extensions.CommandLineUtils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace Snake
{
    [Command(Description = "The snake game.")]
    class Program
    {
        // Command line arguments
        [Option(Description = "Enable low resolution rendering.", ShortName = "lr")]
        public bool LowResolution { get; } = false;

        [Option(Description = "The width of the gameplay grid.")]
        [Range(10, 100)]
        public int Width { get; } = 32;

        [Option(Description = "The height of the gameplay grid.")]
        [Range(10, 100)]
        public int Height { get; } = 24;

        private GraphicsDevice _gd;
        private Sdl2Window _window;
        private CommandList _cl;
        private RgbaFloat _clearColor = new RgbaFloat(0, 0, 0.2f, 1f);
        private SpriteRenderer _spriteRenderer;
        private World _world;
        private Snake _snake;
        private TextRenderer _textRenderer;
        private float _cellSize;
        private Vector2 _worldSize;
        private int _highScore;

        private const float LowResCellSize = 16;
        private const float HighResCellSize = 32;

        public static int Main(string[] args)
        {
            return CommandLineApplication.Execute<Program>(args);
        }

        public int OnExecute()
        {
            _cellSize = LowResolution ? LowResCellSize : HighResCellSize;
            _worldSize = new Vector2(Width, Height);

            Configuration.Default.MemoryManager = new SimpleGcMemoryManager();
            int width = (int)(_worldSize.X * _cellSize);
            int height = (int)(_worldSize.Y * _cellSize);
            WindowCreateInfo wci = new WindowCreateInfo(50, 50, width, height, WindowState.Normal, "Snake");
            GraphicsDeviceOptions options = new GraphicsDeviceOptions();
            _window = new Sdl2Window("Snake", 50, 50, width, height, SDL_WindowFlags.OpenGL, false);
#if DEBUG
            options.Debug = true;
#endif
            options.SyncToVerticalBlank = true;
            options.ResourceBindingModel = ResourceBindingModel.Improved;
            _gd = VeldridStartup.CreateGraphicsDevice(_window, options);
            _cl = _gd.ResourceFactory.CreateCommandList();
            _spriteRenderer = new SpriteRenderer(_gd);

            _window.Resized += () => _gd.ResizeMainWindow((uint)_window.Width, (uint)_window.Height);

            _world = new World(_worldSize, _cellSize);
            _snake = new Snake(_world);
            _textRenderer = new TextRenderer(_gd);
            _textRenderer.DrawText("0");
            _snake.ScoreChanged += () => _textRenderer.DrawText(_snake.Score.ToString());
            _snake.ScoreChanged += () => _highScore = Math.Max(_highScore, _snake.Score);

            Stopwatch sw = Stopwatch.StartNew();
            double previousTime = sw.Elapsed.TotalSeconds;
            while (_window.Exists)
            {
                InputSnapshot snapshot = _window.PumpEvents();
                Input.UpdateFrameInput(snapshot);

                double newTime = sw.Elapsed.TotalSeconds;
                double elapsed = newTime - previousTime;
                previousTime = newTime;
                Update(elapsed);

                if (_window.Exists)
                {
                    DrawFrame();
                }
            }

            _gd.Dispose();

            Console.WriteLine($"Thanks for playing! Your high score was {_highScore}.");
            return _snake.Score;
        }

        private void Update(double deltaSeconds)
        {
            _snake.Update(deltaSeconds);

            if (_snake.Dead && Input.GetKeyDown(Key.Space))
            {
                _snake.Revive();
                _world.CollectFood();
            }
        }

        private void DrawFrame()
        {
            _cl.Begin();
            _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
            _cl.ClearColorTarget(0, _clearColor);

            _snake.Render(_spriteRenderer);
            _world.Render(_spriteRenderer);
            _spriteRenderer.Draw(_gd, _cl);
            Texture targetTex = _textRenderer.TextureView.Target;
            Vector2 textPos = new Vector2(
                (_window.Width / 2f) - targetTex.Width / 2f,
                _window.Height - targetTex.Height - 10f);

            _spriteRenderer.RenderText(_gd, _cl, _textRenderer.TextureView, textPos);

            _cl.End();
            _gd.SubmitCommands(_cl);
            _gd.SwapBuffers(_gd.MainSwapchain);
        }
    }
}
