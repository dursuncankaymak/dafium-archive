# Security Policy

Dafium Archive opens files that may come from untrusted sources, so security bugs are taken seriously.

## Reporting a vulnerability

Please **don't open a public issue**. Use GitHub's private reporting instead:
[Report a vulnerability](https://github.com/dursuncankaymak/dafium-archive/security/advisories/new)

Especially interesting:

- path traversal / "zip slip" that escapes the extraction folder (see `Guvenli` in `Motor/Arsiv.cs`)
- crashes or memory corruption from malformed archives in the native interop (`Motor/UnrarArsiv.cs`, `Motor/SevenZipArsiv.cs`)
- anything that runs code or writes outside the chosen folder

Bugs inside the engines themselves (UnRAR, 7-Zip, libarchive) should also be reported upstream; the bundled DLLs will be updated when fixes land.

## Supported versions

Only the latest release gets fixes.
