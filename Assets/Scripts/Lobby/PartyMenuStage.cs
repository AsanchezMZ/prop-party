using UnityEngine;
using UnityEngine.UIElements;

// Runs in UI Builder as well as the game; keeps the entire composition on screen.
[UxmlElement]
public partial class PartyMenuStage : VisualElement
{
    public PartyMenuStage()
    {
        RegisterCallback<AttachToPanelEvent>(_ =>
        {
            parent.RegisterCallback<GeometryChangedEvent>(Resize);
            Fit(parent.contentRect);
        });
        RegisterCallback<DetachFromPanelEvent>(_ => parent?.UnregisterCallback<GeometryChangedEvent>(Resize));
    }

    void Resize(GeometryChangedEvent change) => Fit(change.newRect);

    void Fit(Rect bounds)
    {
        float scale = Mathf.Min(bounds.width / 1280f, bounds.height / 720f);
        if (scale <= 0) return;
        style.scale = new Scale(new Vector3(scale, scale, 1));
        style.left = (bounds.width - 1280 * scale) * 0.5f;
        style.top = (bounds.height - 720 * scale) * 0.5f;
    }
}
