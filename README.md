**HW Teleportation** implements different teleportation features such as warps & homes.
  
## Permissions

- `hwteleportation.admin` -- for admin commands (/tp & /tphere & /setwarp & /removewarp)  
- `hwteleportation.tpr` -- for teleport request commands (/tpr & /tpa)  
- `hwteleportation.home` -- for home commands (/home, /sethome, /removehome & /homes)  
- `hwteleportation.warp` -- for warp commands (/warp & /warps)  

## Commands

### Teleportation

- `/tpa` -- accept an incoming teleport request  
- `/tpr <player>` -- send a teleport request to another player  
- `/tp <player>` -- teleport yourself to another player  
- `/tp <playerA> <playerB>` -- teleport player A to player B  
- `/tp <x> <y> <z>` -- teleport yourself to a location (~ can be used as x, y, and z respectively, to represent your x, y, and z)
- `/tphere <player>` -- teleport another player to yourself  
 
### Homes
  
- `/home <home>` -- teleport to your home  
- `/homes` -- list all your homes  
- `/sethome <home>` -- set your home location 
- `/removehome <home>` -- remove your home location 

### Warps 

- `/warp <warp>` -- teleport to a warp location
- `/warps` -- list all available warps  
- `/setwarp <warp>` -- set a new warp  
- `/removewarp <warp>` -- remove a warp  

## What are Homes?

This plugin allows players with the permission `hwteleportation.home` to set home locations. **`/sethome <home>`**  
The player can teleport to their home locations. **`/home <home>`**  
You can set up a cooldown in the configuration file so players can only teleport to a home every few minutes.  
  
## What are Warps?

This plugin allows admins with the permission `hwteleportation.admin` to set warp locations. **`/setwarp <warp>`**  
Players with the permission `hwteleportation.warp` can teleport to these locations. **`/warp <warp>`**  
You can set up a cooldown in the configuration file so players can only teleport to a warp every few minutes.  

## Configuration

### Home Count Limit Permissions

In the Home Count Limit permissions section you can add, remove, or edit rows.
Players with the permission on the left-hand side will receive the home count limit set on the right-hand side.

### Default Configuration

```json
{
  "TPR": {
    "Enabled": true,
    "Pending Timer (in seconds)": 30.0,
    "Teleport Timer (in seconds)": 15.0,
    "Cooldown (in minutes)": 5.0
  },
  "Home": {
    "Enabled": true,
    "Check for Stakes (true/false)": true,
    "Stake Detection Radius": 10.0,
    "Teleport Timer (in seconds)": 15.0,
    "Cooldown (in minutes)": 5.0,
    "Home Count Limit": 3,
    "Home Count Limits (granted by permissions)": {
      "HWTeleportation.homelimit.basic": 1,
      "HWTeleportation.homelimit.extended": 2
    }
  },
  "Warp": {
    "Enabled": true,
    "Teleport Timer (in seconds)": 15.0,
    "Cooldown (in minutes)": 10.0
  }
}
```

## Localization
```json
{
  "No Permission": "You don't have permission to use this command.",
  "Request Ran Out": "Your pending teleport request ran out of time.",
  "Request Sent": "Teleport request sent.",
  "Request Got": "{player} would like to teleport to you. Accept by typing /tpa.",
  "Teleported": "You have been teleported to {target}.",
  "Accepted Request": "{player} has accepted your teleport request.",
  "No Pending": "You don't have a pending teleport request.",
  "Already Pending": "{player} already has a teleport request pending.",
  "Teleporting Soon": "You will be teleported in {time} seconds.",
  "Teleport To Self": "You may not teleport to yourself.",
  "No Homes": "You do not have any homes.",
  "Home Set": "You have set your home '{home}'",
  "Home Removed": "You have removed your home '{home}'",
  "Home Exists": "You already have a home called '{home}'",
  "Home Teleported": "You have been teleported to your home '{home}'",
  "Home List": "Your Homes: {homes}",
  "Max Homes": "You may not have more than {count} homes!",
  "Unknown Home": "You don't have a home called '{home}'",
  "Home Compromised": "You are not authorized at any stakes near your home '{home}'. The home was therefore removed.",
  "No Stake": "You need to be close to a stake you're authorized at to set a home.",
  "Home Cooldown": "You need to wait {time} minutes before teleporting to a home again.",
  "TPR Cooldown": "You need to wait {time} minutes before sending the next teleport request.",
  "Warp Set": "You have set warp '{warp}' at your current location.",
  "Warp Removed": "You have removed warp '{warp}'",
  "Warp Teleported": "You have been teleported to warp '{warp}'",
  "Unknown Warp": "There is no warp called '{warp}'",
  "Warp List": "Available Warps: {warps}",
  "Warp Exists": "There already is a warp called '{warp}'",
  "No Warps": "There are no warps set.",
  "Warp Cooldown": "You need to wait {time} minutes before teleporting to a warp again.",
  "Target Location Unsafe": "The target location is currently not safe to teleport to."
}
```