import { SetLogDto } from '../../core/models/training.models';
import { isCalibrationPending } from './calibration';

describe('isCalibrationPending', () => {
  const set = { id: 's1', setNumber: 1 } as SetLogDto;

  it('važi za prvu seriju vežbe koju je server označio', () => {
    expect(isCalibrationPending({ isCalibration: true, setLogs: [] })).toBe(true);
  });

  it('posle upisane prve serije ostatak vežbe ide po planu', () => {
    expect(isCalibrationPending({ isCalibration: true, setLogs: [set] })).toBe(false);
  });

  it('ne važi za vežbu bez oznake, ni kad oznake nema u odgovoru', () => {
    expect(isCalibrationPending({ isCalibration: false, setLogs: [] })).toBe(false);
    expect(isCalibrationPending({ setLogs: [] })).toBe(false);
  });

  it('izmena upisane serije nije nova kalibraciona serija', () => {
    expect(isCalibrationPending({ isCalibration: true, setLogs: [] }, true)).toBe(false);
  });
});
