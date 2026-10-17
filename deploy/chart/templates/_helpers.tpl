{{/*
Nazwa bazowa wszystkich obiektów. Wyprowadzona z nazwy wydania, żeby dwa wydania tego
samego charta w jednym klastrze nie deptały sobie po nazwach.
*/}}
{{- define "app.name" -}}
{{- .Release.Name | trunc 40 | trimSuffix "-" -}}
{{- end -}}

{{/*
Etykiety wspólne dla wszystkich obiektów. `app.kubernetes.io/*` to nazwy uzgodnione
w całym ekosystemie — po nich filtruje k9s, kubectl i większość narzędzi.
*/}}
{{- define "app.labels" -}}
app.kubernetes.io/name: {{ include "app.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

{{/*
Pełna nazwa obrazu. Tag jest wymagany i celowo nie ma wartości domyślnej: brak tagu ma
zatrzymać wdrożenie z czytelnym błędem, a nie po cichu wziąć „latest".
*/}}
{{- define "app.image" -}}
{{- $tag := required "image.tag jest wymagany — ustawia go Argo Application w repo platformy" .Values.image.tag -}}
{{ .Values.image.registry }}/{{ .component }}:{{ $tag }}
{{- end -}}

{{/*
The judge's model as environment variables (appsettings: Models:judge and Judge:Model). Rendered only when
judgeModel.baseUrl or judgeModel.secret is set: without a model the server starts anyway and refuses runs that have
judge checks. With judgeModel.secret the address and the name come from that Secret (app.judgeEnvFrom) and only the
non-secret pool size is rendered here. Used by the server and by the daily live check, so both ask the same model the same way.
*/}}
{{- define "app.judgeEnv" -}}
{{- with .Values.judgeModel }}
{{- if or .baseUrl .secret }}
{{- if not .secret }}
- name: Models__judge__BaseUrl
  value: {{ .baseUrl | quote }}
- name: Models__judge__Model
  value: {{ required "judgeModel.name is required when judgeModel.baseUrl is set" .name | quote }}
{{- end }}
- name: Models__judge__MaxConcurrency
  value: {{ .maxConcurrency | quote }}
{{- end }}
{{- end }}
{{- end -}}

{{/*
The Secret that holds the judge's model address and name (judgeModel.secret), as an envFrom item; empty without one.
*/}}
{{- define "app.judgeEnvFrom" -}}
{{- with .Values.judgeModel.secret }}
- secretRef:
    name: {{ . }}
{{- end }}
{{- end -}}
