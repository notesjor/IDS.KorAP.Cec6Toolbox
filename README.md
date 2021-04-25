# IDS.KorAP.Cec6Toolbox

Die IDS.KorAP.Cec6Toolbox besteht aus folgenden Programmen:

## IDS.KorAP.CatalogBuilder

Dieses Tool durchsucht DeReKo/KorAP-Releases nach den aktuellesten Dateien.
Es werden mehrere Ausgabedateien erzeugt:
- newest.tsv - Diese Datei ist nur für das Programm selbst relevant (temporär).
- size.tsv - Diese Datei gibt Infos über die aktuellen Dateigrößen.
- catalog.tsv - Dies Datei wird von IDS.KorAP.Cec6Processor benötigt, um die entsprechenden Bearbeitungscluster zu berechnen.

## IDS.KorAP.Cec6Processor

Dieses Tool nutzt die catalog.tsv (aus IDS.KorAP.CatalogBuilder) um die Arbeitspakete (cluster) zu planen und durchzuführen (per Aufruf von IDS.KorAP.Cec6Processor.Thread). Bereits exsistierende Dateien /home/ruediger/CEC6 werden ignoriert.

## IDS.KorAP.Cec6Processor.Thread

Wird i. d. R. von IDS.KorAP.Cec6Processor aufgerufen. Kann aber auch isoliert gestartet werden.
Beispiel:

IDS.KorAP.Cec6Processor /current/corpus.zip /output/corpus.cec6.gz

## IDS.KorAP.Cec6Toolbox.TimeModelBuilder

Baut aus den Dateien von IDS.KorAP.Cec6Processor.Thread ein Time-Model.