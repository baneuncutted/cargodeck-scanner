# Cargo Deck Scanner in den Microsoft Store bringen

## 1. Konto anlegen, kostenlos

1. Auf https://storedeveloper.microsoft.com als **Einzelperson** anmelden. Du brauchst ein Microsoft Konto und einen Ausweis zur Bestätigung.
2. Im Partner Center unter **Apps and games** auf **New product** und **MSIX or PWA app**.
3. Den Namen **Cargo Deck Scanner** reservieren. Geht der nicht, zum Beispiel **Cargo Deck Scanner for Star Citizen**, dann auch in `Package/AppxManifest.xml` bei DisplayName anpassen.

## 2. Werte aus dem Partner Center

Die Werte von Product identity sind schon in `Package/AppxManifest.xml` eingetragen.

- Name `CargoDeck.CargoDeckScanner`
- Publisher `CN=FFF76B2B-F752-4A26-B12E-2D33F7D4EEBB`
- PublisherDisplayName `CargoDeck`
- Store Link https://apps.microsoft.com/detail/9NBLSBNSKGFL

## 3. Paket bauen

Im Repo auf **Actions**, links **Store Paket bauen**, rechts **Run workflow**, Version zum Beispiel `1.2.0.0`. Nach ein paar Minuten unten bei **Artifacts** die Zip laden, darin liegt die `.msix`.

## 4. Einreichen

Im Partner Center **Start your submission**

- **Pricing** kostenlos, alle Märkte
- **Properties** Kategorie Utilities and tools. Datenschutz URL `https://cargodeck.onrender.com/datenschutz`
- **Age ratings** Fragebogen ausfüllen, keine Gewalt, keine Käufe, Internet ja
- **Packages** die `.msix` hochladen
- **Store listings** Texte unten einfügen, dazu mindestens 1 Screenshot der App
- **Submission options** bei der Frage zu **runFullTrust** diese Begründung einfügen

> The app is a classic desktop (WinForms) app. It takes a screenshot of the active game window when the user presses a hotkey or enables auto scan, reads the text locally with Windows OCR and sends the recognized trade prices to the user's own Cargo Deck page. Screen capture and the global hotkey check need full trust desktop APIs. No keystrokes are recorded, the app only checks whether its own configured hotkey is pressed.

Die Prüfung dauert meistens 1 bis 3 Tage.

## Store Texte

**Kurzbeschreibung**
Liest Preise und Bestand vom Handelsterminal in Star Citizen und schickt sie an Cargo Deck. Texterkennung läuft lokal auf deinem PC.

**Beschreibung**
Cargo Deck Scanner ist ein kleines Hilfsprogramm für Händler in Star Citizen.

Am Handelsterminal drückst du Linke Strg und eine Taste deiner Wahl, oder du schaltest die Automatik ein. Der Scanner macht dann ein Bild vom Spielfenster, liest Preise und Bestand mit der Texterkennung von Windows und schickt sie an deine Cargo Deck Seite. Dort siehst du sofort die besten Routen mit den neuen Preisen.

So funktioniert es
- Auf cargodeck.onrender.com unter Einstellungen den Kopplungscode kopieren und im Scanner einfügen
- Starten drücken
- Im Spiel am Terminal Linke Strg und ö drücken, oder die Automatik alle paar Sekunden nutzen

Was die App macht und was nicht
- Macht nur ein Bild, wenn du die Taste drückst oder die Automatik läuft
- Die Texterkennung läuft auf deinem PC
- Liest keine Spieldateien, greift nicht ins Spiel ein und drückt keine Tasten
- Offener Quellcode auf GitHub

Inoffizielles Fanprojekt, nicht mit Cloud Imperium Games verbunden.

**Stichworte**
Star Citizen, Trading, Cargo, Handel, Scanner
