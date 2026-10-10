# Tabler Icons

Fonte ufficiale: https://github.com/tabler/tabler-icons

Snapshot: `a4ce1404bc6d24d3c365afe7b258d6bf6f48d62d` (10 ottobre 2026).
Licenza MIT inclusa in `tabler-icons-LICENSE.txt`.
La stessa licenza è inclusa anche nel pacchetto dell'app come asset `licenses/tabler-icons-LICENSE.txt`.

L'app include solo 45 SVG Outline per le categorie, più ricerca e assistente.
I file sono inclusi nella build e non richiedono connessione a Internet.
Il tratto originale resta a 2 px su griglia 24 × 24. `currentColor` è sostituito
con il colore del tema, perché gli SVG sono rasterizzati da MAUI durante la build.

`tabler-icons-map.json` associa le chiavi dell'app ai nomi originali Tabler.
Le 16 chiavi precedenti restano valide e le risorse sono sostituite sul posto:
nessuna modifica delle icone salvate nel database, comprese le scelte personali.
Per riconfermare lo stesso snapshot: `./tools/update-category-icons.ps1`.
