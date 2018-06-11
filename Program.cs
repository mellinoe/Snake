using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using System;
using System.Diagnostics;
using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace Snake
{
    class Program
    {
        private static GraphicsDevice _gd;
        private static Sdl2Window _window;
        private static CommandList _cl;
        private static RgbaFloat _clearColor = new RgbaFloat(0, 0, 0.2f, 1f);
        private static SpriteRenderer _spriteRenderer;
        private static World _world;
        private static Snake _snake;
        private static TextRenderer _textRenderer;
        private const float CellSize = 32;
        private static readonly Vector2 WorldSize = new Vector2(24, 16);

        public static int Main(string[] args)
        {
            Configuration.Default.MemoryManager = new SimpleGcMemoryManager();
            int width = (int)(WorldSize.X * CellSize);
            int height = (int)(WorldSize.Y * CellSize);
            WindowCreateInfo wci = new WindowCreateInfo(50, 50, width, height, WindowState.Normal, "Snake");
            GraphicsDeviceOptions options = new GraphicsDeviceOptions();
#if DEBUG
            options.Debug = true;
#endif
            options.SyncToVerticalBlank = true;
            options.ResourceBindingModel = ResourceBindingModel.Improved;
            VeldridStartup.CreateWindowAndGraphicsDevice(wci, options, out _window, out _gd);
            _cl = _gd.ResourceFactory.CreateCommandList();
            _spriteRenderer = new SpriteRenderer(_gd);

            _window.Resized += () => _gd.ResizeMainWindow((uint)_window.Width, (uint)_window.Height);

            _world = new World(WorldSize, CellSize);
            _snake = new Snake(_world);
            _textRenderer = new TextRenderer(_gd);
            _textRenderer.DrawText("0");
            _snake.ScoreChanged += () => _textRenderer.DrawText(_snake.Score.ToString());

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

            return 0;
        }

        private static void Update(double deltaSeconds)
        {
            _snake.Update(deltaSeconds);

            if (_snake.Dead && Input.GetKeyDown(Key.Space))
            {
                _snake.Revive();
                _world.CollectFood();
            }
        }

        private static void DrawFrame()
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
