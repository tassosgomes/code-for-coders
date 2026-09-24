import { StudentPasswordRecoveryScreen } from '@/features/student-password-recovery/components/student-password-recovery-screen';

export const StudentPasswordRecoveryRoute = () => <StudentPasswordRecoveryScreen mode="request" />;

export const StudentPasswordResetRoute = () => <StudentPasswordRecoveryScreen mode="reset" />;
