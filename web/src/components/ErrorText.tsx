export function ErrorText({ error }: { error: string | undefined }) {
  return error ? (
    <p className="error" role="alert">
      {error}
    </p>
  ) : null;
}
