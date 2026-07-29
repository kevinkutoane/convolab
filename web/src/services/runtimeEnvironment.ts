let runtimeEnvironmentId: string | undefined;

export function setRuntimeEnvironmentId(environmentId?: string) {
  runtimeEnvironmentId = environmentId;
}

export function getRuntimeEnvironmentId() {
  return runtimeEnvironmentId;
}
