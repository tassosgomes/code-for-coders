import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { levelFilterOptions, type ShowcaseLevelParam } from '@/features/student-showcase/utils/showcase-level';

type ShowcaseLevelFilterProps = {
  level: ShowcaseLevelParam | undefined;
  onLevelChange: (level: ShowcaseLevelParam | undefined) => void;
};

const ALL_VALUE = 'todos';

// Radio Option do DS em cartão (P1.a): 180 px no desktop, coluna inteira no mobile.
export const ShowcaseLevelFilter = ({ level, onLevelChange }: ShowcaseLevelFilterProps) => (
  <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
    <p className="w-12 text-sm font-medium text-foreground" id="showcase-level-label">
      Nível
    </p>
    <RadioGroup
      aria-labelledby="showcase-level-label"
      className="grid grid-cols-2 gap-3 sm:flex sm:flex-wrap"
      onValueChange={(value) => onLevelChange(value === ALL_VALUE ? undefined : (value as ShowcaseLevelParam))}
      orientation="horizontal"
      value={level ?? ALL_VALUE}
    >
      {levelFilterOptions.map((option) => {
        const value = option.param ?? ALL_VALUE;
        const id = `showcase-level-${value}`;

        return (
          <label
            className="flex cursor-pointer items-center gap-3 rounded-[10px] border border-border bg-background px-3 py-2.5 text-sm font-medium text-foreground has-[[data-state=checked]]:border-primary has-[[data-state=checked]]:bg-accent sm:w-45"
            htmlFor={id}
            key={value}
          >
            <RadioGroupItem id={id} value={value} />
            {option.label}
          </label>
        );
      })}
    </RadioGroup>
  </div>
);
