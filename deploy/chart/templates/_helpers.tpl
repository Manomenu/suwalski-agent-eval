{{- define "app.name" -}}
{{- .Release.Name | trunc 40 | trimSuffix "-" -}}
{{- end -}}

{{- define "app.labels" -}}
app.kubernetes.io/name: {{ include "app.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

{{- define "app.image" -}}
{{- $tag := required "image.tag jest wymagany — ustawia go Argo Application w repo platformy" .Values.image.tag -}}
{{ .Values.image.registry }}/{{ .component }}:{{ $tag }}
{{- end -}}

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

{{- define "app.modelsEnvFrom" -}}
{{- range $name, $model := .Values.models }}
{{- with $model.secret }}
- secretRef:
    name: {{ . }}
{{- end }}
{{- end }}
{{- end -}}

{{- define "app.anyModel" -}}
{{- range $name, $model := .Values.models }}{{ if or $model.baseUrl $model.secret }}true{{ end }}{{ end }}
{{- end -}}

{{- define "app.anyModelSecret" -}}
{{- range $name, $model := .Values.models }}{{ if $model.secret }}true{{ end }}{{ end }}
{{- end -}}
