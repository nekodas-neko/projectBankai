# Testing together from different places

The game is not on the internet. To play with someone on a different Wi-Fi, use a private network app so your devices behave as if they shared one network. This guide uses Tailscale, which has a free plan for personal, non-commercial use. Check Tailscale's current plan page for user and device limits; at the time of writing the free plan covers a small group of people and unlimited devices.

Nothing about the game changes: one device hosts a session, the others type its address.

## One-time setup

1. The host owner creates a free Tailscale account and installs Tailscale on the host's phone (and PC if hosting from the PC). Sign in. Each device gets an address that starts with `100.`.
2. Invite the other tester to the same tailnet from the Tailscale admin console (Users, Invite). They install Tailscale on their phone and accept the invite.
3. Both turn Tailscale **on** before playing. Check the Tailscale app shows the other device as connected.

## Each session

1. The host opens the game, taps **Session**, then **Host on Wi-Fi**. The menu lists the addresses it can be reached on, for example:
   - `192.168.68.78:47777 (same Wi-Fi)`
   - `100.101.102.103:47777 (Tailscale)`
2. The joiner taps **Session**, types the **Tailscale** address (the `100.` one) into the box, and taps **Join**.
3. Automatic discovery ("Sessions found") only works on the same Wi-Fi. Over Tailscale you always type the address.

## If it does not connect

- Both devices must show Tailscale connected, and both must be in the same tailnet.
- Check the address starts with `100.` and the port is `47777`.
- A PC host on Windows may need its network set to Private, or the game allowed through Windows Firewall. A phone host needs no firewall setup.
- Phone battery savers can pause a VPN in the background; keep the Tailscale app allowed to run.

## Safety

- The session has no password. Only devices in your private Tailscale network can reach it, which is the point; do not open the port on your router.
- Do not use a shared or public tailnet for this.
