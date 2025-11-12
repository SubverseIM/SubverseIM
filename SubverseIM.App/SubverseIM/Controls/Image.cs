using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using Avalonia.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SubverseIM.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SubverseIM.Controls
{
    /// <summary>
    /// Displays a <see cref="SixLabors.ImageSharp.Image"/> image.
    /// </summary>
    public class Image : Control
    {
        /// <summary>
        /// Defines the <see cref="Source"/> property.
        /// </summary>
        public static readonly StyledProperty<SixLabors.ImageSharp.Image?> SourceProperty =
            AvaloniaProperty.Register<Image, SixLabors.ImageSharp.Image?>(nameof(Source));

        /// <summary>
        /// Defines the <see cref="BlendMode"/> property.
        /// </summary>
        public static readonly StyledProperty<BitmapBlendingMode> BlendModeProperty =
            AvaloniaProperty.Register<Image, BitmapBlendingMode>(nameof(BlendMode));

        /// <summary>
        /// Defines the <see cref="Stretch"/> property.
        /// </summary>
        public static readonly StyledProperty<Stretch> StretchProperty =
            AvaloniaProperty.Register<Image, Stretch>(nameof(Stretch), Stretch.Uniform);

        /// <summary>
        /// Defines the <see cref="StretchDirection"/> property.
        /// </summary>
        public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
            AvaloniaProperty.Register<Image, StretchDirection>(
                nameof(StretchDirection),
                StretchDirection.Both);

        /// <summary>
        /// Defines the <see cref="IterationCount"/> property.
        /// </summary>
        public static readonly StyledProperty<IterationCount> IterationCountProperty =
            AvaloniaProperty.Register<Image, IterationCount>(nameof(IterationCount), IterationCount.Infinite);

        static Image()
        {
            AffectsRender<Image>(SourceProperty, IterationCountProperty, StretchProperty, StretchDirectionProperty, BlendModeProperty);
            AffectsMeasure<Image>(SourceProperty, StretchProperty, StretchDirectionProperty);
            AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<Image>(AutomationControlType.Image);
        }

        // Essential bitmap state

        private Image<Bgra32>? sourceBitmap;

        private WriteableBitmap? targetBitmap;

        // Animated bitmap state

        private ImageFrame<Bgra32>? currentFrame;

        private TimeSpan? animationStart;

        /// <summary>
        /// Gets or sets the image that will be displayed.
        /// </summary>
        [Content]
        public SixLabors.ImageSharp.Image? Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        /// <summary>
        /// Gets or sets the blend mode for the image.
        /// </summary>
        public BitmapBlendingMode BlendMode
        {
            get => GetValue(BlendModeProperty);
            set => SetValue(BlendModeProperty, value);
        }

        /// <summary>
        /// Gets or sets a value controlling how the image will be stretched.
        /// </summary>
        public Stretch Stretch
        {
            get => GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        /// <summary>
        /// Gets or sets a value controlling in what direction the image will be stretched.
        /// </summary>
        public StretchDirection StretchDirection
        {
            get => GetValue(StretchDirectionProperty);
            set => SetValue(StretchDirectionProperty, value);
        }

        /// <summary>
        /// Determines how many times the animation (if any) repeats before stopping.
        /// </summary>
        public IterationCount IterationCount 
        {
            get => GetValue(IterationCountProperty);
            set => SetValue(IterationCountProperty, value);
        }

        /// <inheritdoc />
        protected override bool BypassFlowDirectionPolicies => true;

        private void AnimationFrameHandler(TimeSpan elapsed)
        {
            animationStart ??= elapsed;

            var nextFrame = GetCurrentFrame(elapsed);
            if (nextFrame is not null)
            {
                currentFrame = nextFrame;
                InvalidateVisual();
            }
        }

        private ImageFrame<Bgra32>? GetCurrentFrame(TimeSpan elapsed)
        {
            IEnumerable<TimeSpan> frameTimes = Source?.Frames?.GetCumulativeFrameDelays() ?? [];
            TimeSpan duration = frameTimes.LastOrDefault();
            if (duration > TimeSpan.Zero && (IterationCount.IsInfinite || (elapsed - animationStart) / duration < IterationCount.Value))
            {
                return sourceBitmap?.Frames.AsEnumerable().Zip(frameTimes)
                    .FirstOrDefault(x => (elapsed - animationStart)?.Ticks % duration.Ticks < x.Second.Ticks)
                    .First;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Renders the control.
        /// </summary>
        /// <param name="context">The drawing context.</param>
        public unsafe sealed override void Render(DrawingContext context)
        {
            var source = Source;

            if (source is not null && targetBitmap is not null && currentFrame is not null && Bounds.Width > 0 && Bounds.Height > 0)
            {
                Rect viewPort = new Rect(Bounds.Size);
                Avalonia.Size sourceSize = targetBitmap.Size;

                Vector scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
                Avalonia.Size scaledSize = sourceSize * scale;
                Rect destRect = viewPort
                    .CenterRect(new Rect(scaledSize))
                    .Intersect(viewPort);
                Rect sourceRect = new Rect(sourceSize)
                    .CenterRect(new Rect(destRect.Size / scale));

                using (ILockedFramebuffer targetFramebuffer = targetBitmap.Lock())
                {
                    currentFrame.CopyPixelDataTo(new Span<Bgra32>(targetFramebuffer.Address.ToPointer(),
                        targetFramebuffer.Size.Width * targetFramebuffer.Size.Height));
                }

                using (context.PushRenderOptions(new RenderOptions { BitmapBlendingMode = BlendMode }))
                {
                    context.DrawImage(targetBitmap, sourceRect, destRect);
                }
            }

            TopLevel.GetTopLevel(this)?
                .RequestAnimationFrame(AnimationFrameHandler);
        }

        /// <summary>
        /// Measures the control.
        /// </summary>
        /// <param name="availableSize">The available size.</param>
        /// <returns>The desired size of the control.</returns>
        protected override Avalonia.Size MeasureOverride(Avalonia.Size availableSize)
        {
            var source = Source;
            var result = new Avalonia.Size();

            if (source is not null)
            {
                Avalonia.Size sourceSize = new PixelSize(source.Width, source.Height)
                    .ToSizeWithDpi(new Vector(96, 96));
                result = Stretch.CalculateSize(availableSize, sourceSize, StretchDirection);
            }

            return result;
        }

        /// <inheritdoc/>
        protected override Avalonia.Size ArrangeOverride(Avalonia.Size finalSize)
        {
            var source = Source;

            if (source is not null)
            {
                Avalonia.Size sourceSize = new PixelSize(source.Width, source.Height)
                    .ToSizeWithDpi(new Vector(96, 96));
                var result = Stretch.CalculateSize(finalSize, sourceSize);
                return result;
            }
            else
            {
                return new Avalonia.Size();
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            var source = Source;

            if (source is not null && change.Property == SourceProperty)
            {
                ((IDisposable?)change.OldValue)?.Dispose();

                sourceBitmap?.Dispose();
                sourceBitmap = source as Image<Bgra32> ?? source.CloneAs<Bgra32>();

                targetBitmap?.Dispose();
                targetBitmap = new WriteableBitmap(
                    new PixelSize(source.Width, source.Height),
                    new Vector(96, 96), PixelFormat.Bgra8888,
                    AlphaFormat.Unpremul);

                currentFrame = GetCurrentFrame(TimeSpan.Zero) ?? sourceBitmap.Frames.RootFrame;
            }

            if (change.Property == IterationCountProperty || change.Property == SourceProperty)
            {
                animationStart = null;
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ImageAutomationPeer(this);
        }
    }
}