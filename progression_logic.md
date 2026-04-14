# KSP APWorld - Progression Logic

## Starting inventory

The player begins with one each of: command module, solid rocket booster, parachute,
terrestrial-compatible science experiment. These are always accessible regardless of
received items.

## Sphere 0 - no items required

All tier 2 and tier 3 tech locations are in logic from the start.
All KSC level 2 upgrade locations are in logic from the start.
The Kerbin flag location is in logic from the start.

## Mun / Minmus access rule

To place the Mun or Minmus flag locations in logic, the player must have received:
- A Mun Permit or Minmus Permit (respectively), AND
- At least one fuel-bearing part bundle:
  Basic Rocketry, General Rocketry, Advanced Rocketry, Fuel Systems,
  Adv. Fuel Systems, High Altitude Flight, or Large Volume Containment
- At least one engine-bearing part bundle:
  Basic Rocketry, General Rocketry, Heavy Rocketry, or Heavier Rocketry

(Basic Rocketry and General Rocketry satisfy both requirements on their own.)

## Advanced access rule (can_reach_mun_or_minmus)

The following locations require that Mun or Minmus is in logic (see above):
- All tier 4 and above tech locations
- All KSC level 3 upgrade locations

## Outer Kerbol system access rule

Locations outside the Kerbin system (Moho, Eve, Duna, Dres, Jool, Eeloo)
additionally require `can_reach_mun_or_minmus` plus the target body's own SOI permit.

## Moon access rule

A moon's flag location additionally requires the parent planet's SOI permit:
- Gilly requires Eve Permit
- Ike requires Duna Permit
- Laythe, Vall, Tylo, Bop, Pol require Jool Permit

Receiving a moon's permit before the parent planet's permit is valid - the location
simply remains out of logic until both permits are held.
