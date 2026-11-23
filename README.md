# BRCDelivery

<img width="1742" height="961" alt="6u" src="https://github.com/user-attachments/assets/1ad06ef9-8976-441b-b20d-7a5bdf0e3223" />

Adds a Delivery mini game to BRC that earns HyperBoost units for HyperBoostMod

### What is it?

BRCDelivery its a pickup-and-dropoff micro game meant to generate HyperBoost Units for your HyperBoostMod across the vanilla stages (custom maps support coming soon!).

—✰ Generate HyperBoost Units !

—✰ Get Bonuses (or Penalties!)

—✰ Learn your best Time Limit !

To catch a new Delivery open your phone and hit the New Delivery app.

<img width="1130" height="666" alt="image" src="https://github.com/user-attachments/assets/39992f03-18a2-4142-84fa-81c5d15dd3a6" />


A new Pick-Up spot has been randomly chosen. The UI will tell you if the Pick-Up its at the same stage or you need to travel!

<img width="1130" height="666" alt="image" src="https://github.com/user-attachments/assets/e3eb6f22-cb16-4fa3-ad99-b99ba2fa6c54" />

For all your destinations you get:<br>
-Distance Meter to your destination<br>

-Pin in your Phone Map<br>

-Description of Location<br>

<img width="690" height="555" alt="GIhea4h" src="https://github.com/user-attachments/assets/e17b49e0-58c2-43ab-be5a-d4c846f60e95" />


Once you get the Pick-up a new Dropoff location gets pinned and you get 10 in-game minutes to deliver!

<img width="981" height="470" alt="hsrkntsrehn" src="https://github.com/user-attachments/assets/7a62ad27-e814-4a26-83d8-8e251e41b0af" />




Go Fast to avoid penalties, get time bonuses or beat your personal time limit!

Bonuses:

— HyperFunk Delivery (+100 HyperBoost Units)
Delivery under 1 Minute

— BombRush Delivery (+50 HyperBoost Units)
Delivery under 4 Minutes

Penalties:

— Cancelling an order (-200 HyperBoost Units) (default Numpad3)

— Late Deliver = 9+ Minutes Deliver

— Not Delivering = 10 minute limit reached (Insta-kill and/or Insta-broke, first triggers the death sequence and the second will empty your HyperBoost Units wallet entirely)

## REQUIREMENTS

### Configuration Manager recommended

IMPORTANT Note If you have GoVeganUwU Texture and Play Manager: that had the older version of "Bombrush Delivery" included in it, I recommend to update it to its current one — The "Lite" version its now the standard version since the delivery became its own mod (this one), so this way you will not have the two deliveries interfering with each other.

## How to Install?

After downloading the BRCDelivery zip file from releases

**THERE ARE 3 FOLDERS YOU NEED TO COPY !!!**

<img width="786" height="457" alt="image" src="https://github.com/user-attachments/assets/08eba328-5f20-4173-a94a-3d16ada21994" />

1. Copy the plugins/BRCDelivery folder into your plugins folder of BepInEx

<img width="511" height="457" alt="image" src="https://github.com/user-attachments/assets/d7d24a5e-4b3a-4c50-b741-198ba74a0e6d" />
<img width="511" height="457" alt="image" src="https://github.com/user-attachments/assets/d706a5be-b58d-4c1f-9270-0f19b2950e41" />

<img width="743" height="570" alt="image" src="https://github.com/user-attachments/assets/e566fd36-c511-4094-bab0-a12386acdf65" />


2. Now copy the BRCDeliveryAssets folder with the required .cbb assets (these are the floor light and box models) and paste it right in your BepInEx folder !

<img width="511" height="457" alt="image" src="https://github.com/user-attachments/assets/7d4e1ca5-67a7-4065-a76b-f85da6e0ec5d" />

<img width="908" height="548" alt="image" src="https://github.com/user-attachments/assets/9fd2ebbd-3321-45de-bd76-12277a9c525a" />

3. Lastly copy the config/BRCDeliveryLocations folder in the config folder

<img width="511" height="457" alt="image" src="https://github.com/user-attachments/assets/18bdde39-20e1-4bea-8d25-21754151f9b4" />
<img width="511" height="457" alt="image" src="https://github.com/user-attachments/assets/8d2d2aa7-81e5-4ebf-b4ec-c579f889c5e7" />

<img width="747" height="573" alt="image" src="https://github.com/user-attachments/assets/ef0298d4-e124-48c4-937c-fc2fbb2ddb59" />

## Victory Dances & Bunch Of Emotes Support

Set any BRC or BOE animation as your victory dance !

## — Current Stage Only Toggle Option

## — Time Limit Scoring

Press (default Numpad4) to start a personal Time Limit of 20 minutes! Your best time of completed deliveries will get saved.

## How to create new Map locations for the deliveries?

To create a location to be shuffled in the deliveries collection just create it with GeoSaver and add [MAPNAME] between brackets with the correct map name anywhere in the name.

The plugin will shuffle and use any .txt delivery location that you create with GeoSaver, then you must rename to have the following tags anywhere on the name:

[Hideout] or [hideout]
[VersumHill] or [versumhill]
[Mataan] or [mataan]
[BrinkTerminal] or [brinkterminal]
[MilleniumSquare] or [milleniumsquare]
[PyramidIsland] or [pyramidisland]

Example:
[Hideout] Main Area (Billards Table).txt
