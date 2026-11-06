import { SetLogDto } from '../../core/models/training.models';
import { isCalibrationEntry } from './calibration';

describe('isCalibrationEntry', () => {
  const first = { id: 's1', setNumber: 1 } as SetLogDto;
  const second = { id: 's2', setNumber: 2 } as SetLogDto;

  it('važi za prvu seriju vežbe koju je server označio', () => {
    expect(isCalibrationEntry({ isCalibration: true, setLogs: [] })).toBe(true);
  });

  it('posle upisane prve serije nova serija ide po planu', () => {
    expect(isCalibrationEntry({ isCalibration: true, setLogs: [first] })).toBe(false);
  });

  it('ne važi za vežbu bez oznake, ni kad oznake nema u odgovoru', () => {
    expect(isCalibrationEntry({ isCalibration: false, setLogs: [] })).toBe(false);
    expect(isCalibrationEntry({ setLogs: [] })).toBe(false);
  });

  it('izmena prve serije je i dalje kalibraciona serija, izmena druge nije', () => {
    expect(isCalibrationEntry({ isCalibration: true, setLogs: [first, second] }, 1)).toBe(true);
    expect(isCalibrationEntry({ isCalibration: true, setLogs: [first, second] }, 2)).toBe(false);
    expect(isCalibrationEntry({ isCalibration: false, setLogs: [first] }, 1)).toBe(false);
  });
});
