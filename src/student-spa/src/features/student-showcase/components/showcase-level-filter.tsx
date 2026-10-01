import { Label } from '@/components/ui/label';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { levelFilterOptions, type ShowcaseLevelParam } from '@/features/student-showcase/utils/showcase-level';

type ShowcaseLevelFilterProps = {
  level: ShowcaseLevelParam | undefined;
  onLevelChange: (level: ShowcaseLevelParam | undefined) => void;
};

const ALL_VALUE = 'todos';

export const ShowcaseLevelFilter = ({ level, onLevelChange }: ShowcaseLevelFilterProps) => (
  <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:gap-5">
    <p className="text-sm font-medium text-foreground" id="showcase-level-label">
      Nível
    </p>
    <RadioGroup
      aria-labelledby="showcase-level-label"
      className="flex flex-wrap gap-x-6 gap-y-3"
      onValueChange={(value) => onLevelChange(value === ALL_VALUE ? undefined : (value as ShowcaseLevelParam))}
      orientation="horizontal"
      value={level ?? ALL_VALUE}
    >
      {levelFilterOptions.map((option) => {
        const value = option.param ?? ALL_VALUE;
        const id = `showcase-level-${value}`;

        return (
          <div className="flex items-center gap-2" key={value}>
            <RadioGroupItem id={id} value={value} />
            <Label className="cursor-pointer" htmlFor={id}>
              {option.label}
            </Label>
          </div>
        );
      })}
    </RadioGroup>
  </div>
);
