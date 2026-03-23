namespace Dofus210.Bll.Models;

public sealed record CharacterSelectionContext(
    CharacterSummary Character,
    long Experience,
    long Kamas,
    short StatsPoints,
    short SpellsPoints,
    int LifePoints,
    int MaxLifePoints,
    short EnergyPoints,
    short MaxEnergyPoints,
    short ActionPoints,
    short MovementPoints,
    int MapId,
    short CellId,
    byte Direction,
    short SubAreaId);
