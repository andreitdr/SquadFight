using System;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Title + message + confirm/cancel. Configure before it is built via <see cref="UIManager.ShowPopup{T}"/>.</summary>
    public sealed class ConfirmPopup : PopupBase
    {
        public string Title, Message, ConfirmLabel, CancelLabel;
        public Action OnConfirm, OnCancel;
        public UIKit.ButtonStyle ConfirmStyle = UIKit.ButtonStyle.Primary;

        public static ConfirmPopup Show(UIContext ctx, string title, string message, Action onConfirm, Action onCancel = null, bool danger = false) =>
            ctx.Manager.ShowPopup<ConfirmPopup>(p =>
            {
                p.Title = title;
                p.Message = message;
                p.OnConfirm = onConfirm;
                p.OnCancel = onCancel;
                p.ConfirmStyle = danger ? UIKit.ButtonStyle.Danger : UIKit.ButtonStyle.Primary;
            });

        protected override void Build()
        {
            UIKit.Text(Panel, "Title", Title ?? "", Th.fontTitle, Th.text, TextAlignmentOptions.Center, bold: true).Size(preferredHeight: 90);
            if (!string.IsNullOrEmpty(Message)) UIKit.Text(Panel, "Message", Message, Th.fontBody, Th.textDim, TextAlignmentOptions.Center).Size(preferredHeight: 160);
            var row = UIKit.Rect("Buttons", Panel);
            UIKit.HStack(row.gameObject, Th.spacing);
            row.Size(preferredHeight: Th.buttonHeight);
            UIKit.Button(row, "Cancel", CancelLabel ?? Ctx.T("cancel"), () => { Close(); OnCancel?.Invoke(); }, UIKit.ButtonStyle.Ghost);
            UIKit.Button(row, "Confirm", ConfirmLabel ?? Ctx.T("confirm"), () => { Close(); OnConfirm?.Invoke(); }, ConfirmStyle);
        }

        public override bool HandleBack()
        {
            Close();
            OnCancel?.Invoke();
            return true;
        }
    }

    /// <summary>Single message with an OK button (errors, info).</summary>
    public sealed class MessagePopup : PopupBase
    {
        public string Title, Message;
        public Action OnClose;

        public static MessagePopup Show(UIContext ctx, string title, string message, Action onClose = null) =>
            ctx.Manager.ShowPopup<MessagePopup>(p => { p.Title = title; p.Message = message; p.OnClose = onClose; });

        protected override void Build()
        {
            if (!string.IsNullOrEmpty(Title)) UIKit.Text(Panel, "Title", Title, Th.fontTitle, Th.text, TextAlignmentOptions.Center, bold: true).Size(preferredHeight: 90);
            UIKit.Text(Panel, "Message", Message ?? "", Th.fontBody, Th.textDim, TextAlignmentOptions.Center).Size(preferredHeight: 140);
            UIKit.Button(Panel, "Ok", Ctx.T("ok"), () => { Close(); OnClose?.Invoke(); });
        }
    }
}
