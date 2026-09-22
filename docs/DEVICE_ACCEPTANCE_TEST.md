# HASHME v1.2 — Windows Device Acceptance Test

Run these checks on Windows 11 x64 after installation.

1. Close HASHME from its notification-area menu before updating.
2. On Windows 11 x64, install v1.2 and confirm the installer shows progress,
   remains open on a successful completion page, and closes only after **Finish**
   is pressed. Administrator elevation must not be requested.
3. Confirm `%LOCALAPPDATA%\KANE-O\HASHME\HASHME.exe` exists, the Windows Start menu contains **HASHME**, and Windows Installed apps reports **HASHME 1.2.0** with manufacturer **KANE-O**.
4. Separately verify that v1.2 replaces v1.0 or v1.1 through the approved
   UpgradeCode while retaining `settings.json` and `hash-records.json`.
5. Open HASHME and confirm the supplied HashMe mascot is visible before `HASHME`
   on a strip approximately one-third narrower than v1.1.
6. Confirm the HashMe mascot remains sharp and the full strip stays usable at
   100%, 125%, 150%, and 200% Windows display scaling.
7. Drop a small file onto the HashMe mascot, title, status, Clear, hash field, and Copy regions in separate attempts. Confirm every region accepts it.
8. Drop a new small file. Confirm `NEW`, a SHA-256 result, and clipboard copy containing its complete filename and 64-character hash even when the field is visually truncated.
9. Drop the unchanged file again. Confirm orange `EXISTING` and the identical hash.
10. Copy the file to a second name and drop the copy. Confirm orange `EXISTING`.
11. Change the original file and drop it twice. Confirm `CHANGED` both times; it must not silently become `EXISTING`.
12. Right-click and approve **Replace stored record with current hash…**. Drop it again and confirm `EXISTING`.
13. Drop several files together. Confirm all hashes share one display line and Copy returns every complete filename and hash on one clipboard line.
14. Press **CLEAR**. Confirm the visible result disappears, then drop a previously recorded file and confirm its history remains `EXISTING`.
15. Arm **Pair Match — next drop** and drop two identical files. Confirm `MATCH`.
16. Arm Pair Match again and drop different files. Confirm `DIFFERENT`.
17. Test HashMe Dark, Graphite, Light, and High Contrast. Confirm the HashMe mascot,
    text, tick marks, buttons, highlights, and status badges remain visible.
18. Toggle always-on-top, lock, each theme, and Start with Windows; restart
    HASHME and confirm the persistent settings.
19. Hide HASHME and restore it by double-clicking its notification-area icon.
20. Confirm the source files' sizes, contents, and last-write timestamps were not
    changed by hashing.
