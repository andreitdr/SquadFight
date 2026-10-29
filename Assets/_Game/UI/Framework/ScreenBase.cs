using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// A full-screen view. Built in code by <see cref="Build"/>; refreshed in <see cref="OnShow"/>.
    /// Views never mutate services directly – they call view-model commands.
    /// </summary>
    public abstract class ScreenBase : MonoBehaviour
    {
        protected UIContext Ctx { get; private set; }
        protected Theme Th => Ctx.Theme;
        public RectTransform Root { get; private set; }
        public CanvasGroup Group { get; private set; }
        public object Args { get; private set; }

        public virtual bool ShowTopBar => true;
        public virtual bool ShowTabBar => true;
        /// <summary>Tab highlighted in the bottom bar (null = none).</summary>
        public virtual string TabId => null;

        internal void Init(UIContext ctx, object args)
        {
            Ctx = ctx;
            Args = args;
            Root = (RectTransform)transform;
            Group = gameObject.AddComponent<CanvasGroup>();
            Build();
        }

        /// <summary>Create the hierarchy (once).</summary>
        protected abstract void Build();

        /// <summary>Became the top screen (after push or after the screen above was popped).</summary>
        public virtual void OnShow() { }

        /// <summary>Another screen was pushed on top, or this one is being removed.</summary>
        public virtual void OnHide() { }

        /// <summary>Android back / Escape. Return true if handled (otherwise the manager pops this screen).</summary>
        public virtual bool HandleBack() => false;

        /// <summary>Called right before destruction (dispose view models here).</summary>
        public virtual void OnClosed() { }

        protected void Close() => Ctx.Manager.Pop(this);
    }

    /// <summary>A modal on top of screens: dimmed backdrop + centred panel built by <see cref="Build"/>.</summary>
    public abstract class PopupBase : MonoBehaviour
    {
        protected UIContext Ctx { get; private set; }
        protected Theme Th => Ctx.Theme;
        protected RectTransform Panel { get; private set; }
        public CanvasGroup Group { get; private set; }
        public virtual bool CloseOnBackdrop => true;
        public virtual float PanelWidth => 940;

        internal void Init(UIContext ctx)
        {
            Ctx = ctx;
            var rt = (RectTransform)transform;
            rt.Stretch();
            Group = gameObject.AddComponent<CanvasGroup>();
            UIKit.ClickArea(transform, "Backdrop", () => { if (CloseOnBackdrop) Close(); }, ctx.Theme.overlay);
            var panelImg = UIKit.Panel(transform, "Panel", ctx.Theme.panel, raycast: true);
            Panel = panelImg.rectTransform;
            Panel.anchorMin = Panel.anchorMax = Panel.pivot = new Vector2(0.5f, 0.5f);
            Panel.sizeDelta = new Vector2(PanelWidth, 0);
            UIKit.VStack(Panel.gameObject, ctx.Theme.spacing, ctx.Theme.padding);
            var fitter = Panel.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            Build();
        }

        protected abstract void Build();

        public virtual bool HandleBack()
        {
            Close();
            return true;
        }

        public virtual void OnClosed() { }

        public void Close() => Ctx.Manager.ClosePopup(this);
    }
}
