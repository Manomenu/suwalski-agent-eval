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
The models Suwageval itself calls (values: models; appsettings: Models:<name>) as environment variables, one entry each:
fallback, which every role uses unless it has its own, and the roles judge, simulator and describer. An entry renders only
when its baseUrl or secret is set; with a secret the address and the name come from that Secret (app.modelsEnvFrom) and only
the non-secret pool size is rendered here. Used by the server and by the daily live check, so both ask the same models.
*/}}
{{- define "app.modelsEnv" -}}
{{- range $name, $model := .Values.models }}
{{- if or $model.baseUrl $model.secret }}
{{- if not $model.secret }}
- name: Models__{{ $name }}__BaseUrl
  value: {{ $model.baseUrl | quote }}
- name: Models__{{ $name }}__Model
  value: {{ required (printf "models.%s.name is required when models.%s.baseUrl is set" $name $name) $model.name | quote }}
{{- end }}
- name: Models__{{ $name }}__MaxConcurrency
  value: {{ $model.maxConcurrency | quote }}
{{- end }}
{{- end }}
{{- end -}}

{{/*
The Secrets that hold the models' addresses and names (models.<name>.secret), as envFrom items; empty without any.
*/}}
{{- define "app.modelsEnvFrom" -}}
{{- range $name, $model := .Values.models }}
{{- with $model.secret }}
- secretRef:
    name: {{ . }}
{{- end }}
{{- end }}
{{- end -}}

{{/*
"true" when a model is configured at all (its baseUrl or secret set), else empty.
*/}}
{{- define "app.anyModel" -}}
{{- range $name, $model := .Values.models }}{{ if or $model.baseUrl $model.secret }}true{{ end }}{{ end }}
{{- end -}}

{{/*
"true" when a model's address comes from a Secret, else empty.
*/}}
{{- define "app.anyModelSecret" -}}
{{- range $name, $model := .Values.models }}{{ if $model.secret }}true{{ end }}{{ end }}
{{- end -}}
