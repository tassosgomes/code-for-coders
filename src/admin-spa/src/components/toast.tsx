import { CircleCheck } from 'lucide-react';

type ToastProps = { message: string };

export const Toast = ({ message }: ToastProps) => <p className="app-toast" role="status"><CircleCheck size={16} aria-hidden="true" />{message}</p>;
