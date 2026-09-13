using System.Collections.Generic;
using UnityEngine;

namespace AstraExpress
{
    public sealed partial class AstraGame
    {
        private readonly Dictionary<string, Texture2D> interfaceIcons = new Dictionary<string, Texture2D>();
        private GUIStyle interfaceLabel;
        private GUIStyle interfaceSmall;
        private GUIStyle interfaceBadge;
        private readonly Color interfaceSurface = new Color(0.075f, 0.14f, 0.18f);
        private readonly Color interfaceBorder = new Color(0.21f, 0.34f, 0.38f);

        private void Styles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold, normal = { textColor = ink } };
            headingStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = ink }, wordWrap = true };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = ink }, wordWrap = true };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = muted }, wordWrap = true };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = ink } };
            // Native button textures multiply the tint and wash out the dark palette.
            // A transparent control keeps IMGUI hit testing; we draw every visual state.
            buttonStyle = new GUIStyle { padding = new RectOffset(), margin = new RectOffset() };
            interfaceLabel = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(), normal = { textColor = ink } };
            interfaceSmall = new GUIStyle(interfaceLabel) { fontSize = 10, fontStyle = FontStyle.Normal, normal = { textColor = muted } };
            interfaceBadge = new GUIStyle(interfaceSmall) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }

        private static void Fill(Rect rectangle, Color color)
        {
            Color previous = GUI.color;
            // These palette values are display/sRGB colors. IMGUI's untextured tint
            // reaches the linear render target, so convert once to avoid pale panels.
            GUI.color = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            GUI.DrawTexture(rectangle, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void Panel(Rect rectangle)
        {
            Fill(rectangle, new Color(0.035f, 0.085f, 0.115f, 0.96f));
            Fill(new Rect(rectangle.x, rectangle.y, rectangle.width, 2), interfaceBorder);
        }

        private bool InterfaceHit(Rect rectangle, string name, bool enabled)
        {
            bool previous = GUI.enabled;
            GUI.enabled = previous && enabled;
            GUI.SetNextControlName(name);
            bool clicked = GUI.Button(rectangle, GUIContent.none, buttonStyle);
            GUI.enabled = previous;
            if (clicked) coachInputResumeFrame = Time.frameCount + 1;
            return clicked;
        }

        private void InterfaceSurface(Rect rectangle, bool active, bool enabled, Color accent, string controlName)
        {
            bool hover = enabled && rectangle.Contains(Event.current.mousePosition);
            bool focused = enabled && GUI.GetNameOfFocusedControl() == controlName;
            Color border = !enabled ? new Color(0.14f, 0.23f, 0.27f) : active || focused ? accent : hover ? new Color(0.43f, 0.66f, 0.66f) : interfaceBorder;
            Color surface = !enabled ? new Color(0.06f, 0.11f, 0.14f) : active ? new Color(0.08f, 0.25f, 0.27f) : hover ? new Color(0.12f, 0.23f, 0.27f) : interfaceSurface;
            Fill(rectangle, border);
            Fill(new Rect(rectangle.x + 1, rectangle.y + 1, rectangle.width - 2, rectangle.height - 2), surface);
            if (active) Fill(new Rect(rectangle.x + 1, rectangle.yMax - 3, rectangle.width - 2, 2), accent);
        }

        private void InterfaceIcon(Rect rectangle, string name, bool enabled = true)
        {
            if (!interfaceIcons.TryGetValue(name, out Texture2D icon))
            {
                icon = Resources.Load<Texture2D>("UI/Icons/" + name);
                interfaceIcons[name] = icon;
            }
            if (icon == null) return; // Labels and controls remain usable before art is imported.
            Color previous = GUI.color;
            GUI.color = new Color(1, 1, 1, enabled ? 1 : 0.38f);
            GUI.DrawTexture(rectangle, icon, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }

        private string InterfaceIconName(string caption)
        {
            string text = caption.ToLowerInvariant();
            if (text.Contains("hide") || text.Contains("close")) return "close";
            if (text.Contains("restart") || text.Contains("confirm")) return "restart";
            if (text.Contains("resume") || text.Contains("dispatch")) return "play";
            if (text.Contains("pause")) return "pause";
            if (text.Contains("park")) return "park";
            if (text.Contains("upgrade") || text.Contains("capacity") || text.Contains("max level")) return "upgrade";
            if (text.Contains("power") || text.Contains("conduit")) return "conduit";
            if (text.Contains("rail")) return "rail";
            if (text.Contains("train") || text.Contains("fleet")) return "train";
            if (text.Contains("rover") || text.Contains("explor")) return "rover";
            if (text.Contains("colony")) return "colony";
            return "focus";
        }

        private bool Button(Rect rectangle, string caption, bool active = false, bool enabled = true)
        {
            string name = "Astra-" + caption + "-" + rectangle.x + "-" + rectangle.y;
            bool clicked = InterfaceHit(rectangle, name, enabled);
            InterfaceSurface(rectangle, active, enabled, cyan, name);
            string text = caption == "HIDE  X" ? "Hide" : caption;
            if (caption == "<" || caption == ">")
            {
                GUI.Label(rectangle, caption, labelStyle);
                return clicked;
            }
            float iconSize = rectangle.height < 30 ? 16 : 20;
            InterfaceIcon(new Rect(rectangle.x + 8, rectangle.center.y - iconSize / 2, iconSize, iconSize), InterfaceIconName(caption), enabled);
            interfaceLabel.fontSize = rectangle.width < 130 ? 11 : 13;
            interfaceLabel.normal.textColor = enabled ? ink : muted;
            interfaceLabel.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(rectangle.x + iconSize + 13, rectangle.y + 2, rectangle.width - iconSize - 20, rectangle.height - 4), text, interfaceLabel);
            interfaceLabel.alignment = TextAnchor.MiddleLeft;
            interfaceLabel.fontSize = 13;
            interfaceLabel.normal.textColor = ink;
            return clicked;
        }

        private bool ToolbarCard(Rect rectangle, string title, string subtitle, string icon, string key, bool active, Color accent)
        {
            string name = "Astra-tool-" + title;
            bool clicked = InterfaceHit(rectangle, name, true);
            InterfaceSurface(rectangle, active, true, accent, name);
            InterfaceIcon(new Rect(rectangle.x + 7, rectangle.y + 6, 32, 32), icon);
            GUI.Label(new Rect(rectangle.x + 45, rectangle.y + 6, rectangle.width - (string.IsNullOrEmpty(key) ? 49 : 66), 17), title, interfaceLabel);
            GUI.Label(new Rect(rectangle.x + 45, rectangle.y + 25, rectangle.width - 49, 14), subtitle, interfaceSmall);
            if (!string.IsNullOrEmpty(key))
            {
                var badge = new Rect(rectangle.xMax - 21, rectangle.y + 5, 15, 16);
                Fill(badge, new Color(0.16f, 0.26f, 0.29f));
                GUI.Label(badge, key, interfaceBadge);
            }
            return clicked;
        }

        private void Readiness(Rect rectangle, bool powered, bool rails, bool running, bool served, bool parking)
        {
            string[] names = { "Power", "Rails", "Train" };
            string[] states = { powered ? "Linked" : "Missing", rails ? "Linked" : "Missing", parking ? "Parking" : running ? served ? "Running" : "Busy" : powered && rails ? "Ready" : "Waiting" };
            bool[] ready = { powered, rails, !parking && (served || !running && powered && rails) };
            float width = (rectangle.width - 24) / 3;
            for (int i = 0; i < 3; i++)
            {
                var card = new Rect(rectangle.x + i * (width + 12), rectangle.y, width, rectangle.height);
                Fill(card, interfaceSurface);
                Fill(new Rect(card.x, card.y, card.width, 2), ready[i] ? cyan : gold);
                interfaceBadge.fontSize = 11;
                interfaceBadge.normal.textColor = ink;
                GUI.Label(new Rect(card.x, card.y + 5, card.width, 16), names[i], interfaceBadge);
                interfaceBadge.fontSize = 10;
                interfaceBadge.normal.textColor = ready[i] ? cyan : gold;
                GUI.Label(new Rect(card.x, card.y + 22, card.width, 15), states[i], interfaceBadge);
                if (i < 2) GUI.Label(new Rect(card.xMax, card.y + 10, 12, 20), ">", interfaceBadge);
            }
            interfaceBadge.normal.textColor = muted;
        }

        private string TrainStatus(FreightTrain train = null)
        {
            train = train ?? Simulation.Trains[Mathf.Clamp(selectedTrainIndex, 0, Simulation.Trains.Count - 1)];
            if (train.Phase == TrainPhase.Parked) return "Parked at colony";
            if (train.Phase == TrainPhase.Unloading && train.Resource == ResourceKind.Fluxite && train.Cargo > 0 && train.Destination != null && train.Destination.Stock >= train.Destination.Storage) return "Plant full: waiting";
            if (train.Phase == TrainPhase.ReturningToDepot) return "Returning to depot";
            if (train.ParkRequested) return "Parking after delivery";
            switch (train.Phase)
            {
                case TrainPhase.ToMine: return "Travelling to mine";
                case TrainPhase.Loading: return train.Resource == ResourceKind.Fluxite ? "Loading Fluxite" : "Loading ore";
                case TrainPhase.ToColony: return train.Resource == ResourceKind.Fluxite ? "Delivering to plant" : "Returning to colony";
                case TrainPhase.Unloading: return train.Resource == ResourceKind.Fluxite ? "Unloading Fluxite" : "Unloading ore";
                default: return "Running";
            }
        }
    }
}
