using FamilyCloud.Contracts.Calendars;

namespace FamilyCloud.UI;

public enum EventEditDialogAction
{
    Save,
    Delete,
}

public sealed record EventEditDialogResult(EventEditDialogAction Action, EventWriteRequest? Request = null, Guid? CalendarId = null);
