using PriceTrackerWorkspace.Scraping;

namespace PriceTrackerWorkspace.Components.Shared;

public sealed record PickerSavedEventArgs(VisualSelectionResult Result, bool RecheckDomainItems);
