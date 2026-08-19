using OurLive.Contracts.Calendars;

namespace OurLive.UI;

public enum EventEditDialogAction
{
    Save,
    Delete,
}

public sealed record EventEditDialogResult(EventEditDialogAction Action, EventWriteRequest? Request = null);
