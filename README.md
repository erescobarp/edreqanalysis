# EDReqAnalysis
Sistema de análisis de deuda sociotécnica en requerimientos
## Versiones
1.0: Primera versión
## Descripción
Utiliza el dataset de Jira propuesto por Ortu et al. (2015) en su trabajo "The JIRA Repository Dataset: Understanding Social Aspects of Software Development" como base para analizar de forma automática la deuda sociotécnica en los requerimientos.
## Instalación

 - Agregue la cadana de conexión en el archivo App.config en el elemento "jira"
 - Puede añadir nuevos "smells" de requerimientos en el diccionario presenta en la base de datos SQLite llamada "smells_catalog.db"
 ## Referencias
 
 - Ortu, M., Destefanis, G., Adams, B., Murgia, A., Marchesi, M., & Tonelli, R. (2015). _The JIRA Repository Dataset_. 1–4. https://doi.org/10.1145/2810146.2810147
