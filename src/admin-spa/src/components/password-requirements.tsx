type PasswordRequirementsProps = { password: string };

export const PasswordRequirements = ({ password }: PasswordRequirementsProps) => {
  const requirements = [
    ['8+ caracteres', password.length >= 8],
    ['Letra maiúscula', /[A-Z]/.test(password)],
    ['Letra minúscula', /[a-z]/.test(password)],
    ['Número', /\d/.test(password)],
    ['Símbolo', /[^\w\s]/.test(password)],
  ] as const;

  return <ul aria-label="Requisitos da senha" className="password-requirements">{requirements.map(([label, met]) => <li className={met ? 'met' : ''} key={label}><span aria-hidden="true">{met ? '✓' : '○'}</span>{label}</li>)}</ul>;
};
