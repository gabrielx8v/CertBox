# CertBox privacy

Updated: November 17, 2026.

CertBox processes certificates and keys locally. The app does not send files, their contents or passwords to the internet. It requires no account and includes no advertising, telemetry, usage analytics or automatic update checks.

Selected files and entered passwords are used in memory for inspection, certificate/key matching and export. Exported files are written to locations chosen by the user. CertBox does not install certificates in the system.

The only setting persisted is the language, in %LOCALAPPDATA%\CertBox\settings.json. On Linux, the same setting uses $XDG_CONFIG_HOME/CertBox/settings.json or ~/.config/CertBox/settings.json. Certificates, keys, passwords and file history are not saved in that file. Deleting the file restores the initial language choice.

Saving uses temporary files in the destination directory and, when necessary, transient backups to restore outputs after an error. These files are removed when the operation completes normally. Abrupt process or computer termination can leave transient files in the destination. The runtime can also extract its own libraries to an execution cache directory; this extraction does not contain loaded certificates or keys.

Clicking a project link opens GitHub in your default browser, subject to that site’s privacy policy. CertBox does not send your certificates, keys or passwords to GitHub. Use fictional files to report problems and keep real data private.

The code, interface and icon were produced using AI during development. CertBox does not use AI services while running and does not send certificate data to those services. Third-party libraries retain their authorship and licenses.

For questions or requests, use the support page of the official repository shown in the app's About section and on the distributed release page.
