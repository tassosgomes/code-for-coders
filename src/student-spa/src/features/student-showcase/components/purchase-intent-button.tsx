import { Link } from 'react-router';

import { Button } from '@/components/ui/button';
import { paths } from '@/config/paths';

type PurchaseIntentButtonProps = { offerId: string; offerName: string; courseId: string };
export const PurchaseIntentButton = ({ offerId, offerName, courseId }: PurchaseIntentButtonProps) => <Button asChild><Link aria-label={`Comprar ${offerName}`} to={paths.studentPurchase.getHref(offerId, courseId)}>Comprar</Link></Button>;
