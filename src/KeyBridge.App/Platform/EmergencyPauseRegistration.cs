using System;
using KeyBridge.Core;

namespace KeyBridge.App.Platform;

/// <summary>Identifies the exact successful emergency shortcut registration that was exercised.</summary>
public sealed record EmergencyPauseRegistration(Guid Id, KeyChord Chord);
